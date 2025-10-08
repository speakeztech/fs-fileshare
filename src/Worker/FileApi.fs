module FS.FileShare.Worker.FileApi

open Fable.Core
open Fable.Core.JsInterop
open CloudFlare.Worker.Context
open CloudFlare.Worker.Context.Globals
open CloudFlare.Worker.Context.Helpers
open CloudFlare.R2
open FS.FileShare.Worker.Types
open FS.FileShare.Worker.R2Helpers

let API_PREFIX = "/api"

[<Emit("new URL($0)")>]
let createURL(url: string) : obj = jsNative

[<Emit("JSON.stringify($0)")>]
let jsonStringify(obj: obj) : string = jsNative

[<Emit("JSON.parse($0)")>]
let jsonParse(str: string) : obj = jsNative

[<Emit("new Uint8Array()")>]
let createEmptyUint8Array() : JS.ArrayBuffer = jsNative

/// Extract resource path from API request URL
let makeResourcePath (request: Request) : string =
    let url = Request.getUrl request
    let urlObj: obj = createURL(url)
    let mutable path: string = urlObj?pathname

    // Remove API prefix (e.g., "/api")
    if path.StartsWith(API_PREFIX) then
        path <- path.Substring(API_PREFIX.Length)

    // Remove leading slash
    if path.StartsWith("/") then
        path <- path.Substring(1)

    // Remove action prefix (upload/, mkdir/, etc.)
    // Path format is: /api/upload/actual/file/path or /api/mkdir/folder/name
    let firstSlash = path.IndexOf('/')
    if firstSlash >= 0 then
        path <- path.Substring(firstSlash + 1)
    else
        path <- ""

    // Decode URI components
    path <- JS.decodeURIComponent(path)

    path

/// Create JSON response
let createJsonResponse (data: obj) (status: int) : Response =
    let headers = Headers.Create()
    headers.set("Content-Type", "application/json")

    let init = createObj [
        "status" ==> status
        "headers" ==> headers
    ]

    Response.Create(U2.Case1 (jsonStringify data), unbox init)

/// Handle GET /api/list?path=... - List directory contents
let handleList (request: Request) (bucket: R2Bucket) : JS.Promise<Response> =
    promise {
        let url = Request.getUrl request
        let urlObj: obj = createURL(url)
        let searchParams: obj = urlObj?searchParams
        let pathParam: string = searchParams?get("path") |> Option.ofObj |> Option.defaultValue ""

        let prefix = if pathParam = "" then "" else pathParam.TrimEnd('/') + "/"
        let! objects = listAll bucket prefix false

        let fileInfos =
            objects
            |> Array.map toFileInfo
            |> Array.map (fun fi ->
                createObj [
                    "Name" ==> fi.Name
                    "Path" ==> fi.Path
                    "Size" ==> fi.Size
                    "IsDirectory" ==> fi.IsDirectory
                    "ContentType" ==> (fi.ContentType |> Option.toObj)
                    "LastModified" ==> fi.LastModified
                    "ETag" ==> (fi.ETag |> Option.toObj)
                ]
            )

        return createJsonResponse (createObj ["files" ==> fileInfos]) 200
    }

/// Handle POST /api/upload - Upload file
let handleUpload (request: Request) (bucket: R2Bucket) : JS.Promise<Response> =
    promise {
        let path = makeResourcePath request

        if path = "" then
            return createJsonResponse (createObj ["error" ==> "Path required"]) 400
        else
            let! body = request.arrayBuffer()
            let contentType = request.headers.get("Content-Type")

            let putOptions =
                match contentType with
                | Some ct ->
                    createObj [
                        "httpMetadata" ==> createObj ["contentType" ==> ct]
                    ] |> unbox<R2PutOptions>
                | None ->
                    createObj [] |> unbox<R2PutOptions>

            let! obj = bucket.put(path, U3.Case1 body, putOptions)
            return createJsonResponse (createObj ["success" ==> true; "path" ==> path]) 201
    }

/// Handle DELETE /api/delete?path=... - Delete file or directory
let handleDeleteApi (request: Request) (bucket: R2Bucket) : JS.Promise<Response> =
    promise {
        let url = Request.getUrl request
        let urlObj: obj = createURL(url)
        let searchParams: obj = urlObj?searchParams
        let pathParam: string option = searchParams?get("path") |> Option.ofObj

        match pathParam with
        | None ->
            return createJsonResponse (createObj ["error" ==> "Path required"]) 400
        | Some path ->
            let! obj = bucket.head(path)

            match obj with
            | None ->
                return createJsonResponse (createObj ["error" ==> "Not found"]) 404
            | Some r2obj ->
                // Check if it's a directory
                let isDirectory =
                    match r2obj.customMetadata with
                    | Some meta ->
                        let rt: string option = meta?resourcetype
                        rt = Some "<collection />"
                    | None -> false

                if isDirectory then
                    // Delete all objects with this prefix
                    do! deletePrefix bucket (path + "/")
                    do! bucket.delete(path)
                else
                    do! bucket.delete(path)

                return createJsonResponse (createObj ["success" ==> true]) 200
    }

/// Handle POST /api/mkdir - Create directory
let handleMkdir (request: Request) (bucket: R2Bucket) : JS.Promise<Response> =
    promise {
        let path = makeResourcePath request

        if path = "" then
            return createJsonResponse (createObj ["error" ==> "Path required"]) 400
        else
            // Check if already exists
            let! existing = bucket.head(path)

            match existing with
            | Some _ ->
                return createJsonResponse (createObj ["error" ==> "Already exists"]) 409
            | None ->
                // Create directory marker with empty Uint8Array
                let options =
                    createObj [
                        "customMetadata" ==> createObj ["resourcetype" ==> "<collection />"]
                    ]
                    |> unbox<R2PutOptions>

                let emptyBody = createEmptyUint8Array()
                let! _ = bucket.put(path, U3.Case1 emptyBody, options)
                return createJsonResponse (createObj ["success" ==> true; "path" ==> path]) 201
    }

/// Handle GET /api/download?path=... - Download file
let handleDownload (request: Request) (bucket: R2Bucket) : JS.Promise<Response> =
    promise {
        let url = Request.getUrl request
        let urlObj: obj = createURL(url)
        let searchParams: obj = urlObj?searchParams
        let pathParam: string option = searchParams?get("path") |> Option.ofObj

        match pathParam with
        | None ->
            return createJsonResponse (createObj ["error" ==> "Path required"]) 400
        | Some path ->
            let! obj = bucket.get(path)

            match obj with
            | None ->
                return createJsonResponse (createObj ["error" ==> "Not found"]) 404
            | Some r2obj ->
                let headers = Headers.Create()
                match r2obj.httpMetadata with
                | Some meta ->
                    match meta.contentType with
                    | Some ct -> headers.set("Content-Type", ct)
                    | None -> ()
                | None -> ()

                headers.set("ETag", r2obj.etag)
                headers.set("Last-Modified", toRfc1123 r2obj.uploaded)
                headers.set("Content-Disposition", $"attachment; filename=\"{r2obj.key.Split('/') |> Array.last}\"")

                let init = createObj [
                    "status" ==> 200
                    "headers" ==> headers
                ]

                let streamBody: obj = r2obj.body
                return Response.Create(unbox streamBody, unbox init)
    }

/// Dispatch API request to appropriate handler
let dispatchApiHandler (request: Request) (bucket: R2Bucket) : JS.Promise<Response> =
    promise {
        let url = Request.getUrl request
        let urlObj: obj = createURL(url)
        let pathname: string = urlObj?pathname
        let method = Request.getMethod request

        return!
            match pathname, method with
            | p, "GET" when p.StartsWith(API_PREFIX + "/list") -> handleList request bucket
            | p, "GET" when p.StartsWith(API_PREFIX + "/download") -> handleDownload request bucket
            | p, "POST" when p.StartsWith(API_PREFIX + "/upload") -> handleUpload request bucket
            | p, "DELETE" when p.StartsWith(API_PREFIX + "/delete") -> handleDeleteApi request bucket
            | p, "POST" when p.StartsWith(API_PREFIX + "/mkdir") -> handleMkdir request bucket
            | _ -> Promise.lift (createJsonResponse (createObj ["error" ==> "Not found"]) 404)
    }
