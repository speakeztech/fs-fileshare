module FS.FileShare.Pages.FileApi

open Fable.Core
open Fable.Core.JsInterop
open Browser.Types

[<Emit("fetch($0, $1)")>]
let fetchRaw (url: string) (options: obj) : JS.Promise<obj> = jsNative

// Get the Worker API base URL from environment variable or use default
[<Emit("import.meta.env.VITE_WORKER_URL || 'https://fs-fileshare-worker.engineering-0c5.workers.dev'")>]
let private getWorkerUrl() : string = jsNative

let private workerBaseUrl = getWorkerUrl()

type FileInfo = {
    Name: string
    Path: string
    Size: int64
    IsDirectory: bool
    ContentType: string option
    LastModified: string
    ETag: string option
}

[<Emit("btoa($0)")>]
let btoa (str: string) : string = jsNative

/// Get basic auth header
let getAuthHeader (username: string) (password: string) : string =
    let credentials = $"{username}:{password}"
    let encoded = btoa credentials
    $"Basic {encoded}"

/// List files in a directory
let listFiles (path: string option) (username: string) (password: string) : JS.Promise<Result<FileInfo array, string>> =
    promise {
        try
            let queryPath = path |> Option.defaultValue ""
            let url = $"{workerBaseUrl}/api/list?path={JS.encodeURIComponent queryPath}"

            let options = createObj [
                "method" ==> "GET"
                "headers" ==> createObj [
                    "Authorization" ==> getAuthHeader username password
                ]
            ]

            let! response = fetchRaw url options
            let statusCode: int = response?status

            if statusCode = 200 then
                let! json = response?text()
                let data = JS.JSON.parse(json)
                let files: FileInfo array = data?files
                return Ok files
            else
                return Error $"HTTP {statusCode}"
        with
        | ex -> return Error $"Exception: {ex.Message}"
    }

/// Upload a file
let uploadFile (path: string) (file: File) (username: string) (password: string) : JS.Promise<Result<unit, string>> =
    promise {
        try
            let url = $"{workerBaseUrl}/api/upload/{JS.encodeURIComponent path}"

            let options = createObj [
                "method" ==> "POST"
                "headers" ==> createObj [
                    "Authorization" ==> getAuthHeader username password
                    "Content-Type" ==> file.``type``
                ]
                "body" ==> file
            ]

            let! response = fetchRaw url options
            let statusCode: int = response?status

            if statusCode >= 200 && statusCode < 300 then
                return Ok ()
            else
                return Error $"HTTP {statusCode}"
        with
        | ex -> return Error $"Exception: {ex.Message}"
    }

/// Delete a file or directory
let deleteItem (path: string) (username: string) (password: string) : JS.Promise<Result<unit, string>> =
    promise {
        try
            let url = $"{workerBaseUrl}/api/delete?path={JS.encodeURIComponent path}"

            let options = createObj [
                "method" ==> "DELETE"
                "headers" ==> createObj [
                    "Authorization" ==> getAuthHeader username password
                ]
            ]

            let! response = fetchRaw url options
            let statusCode: int = response?status

            if statusCode >= 200 && statusCode < 300 then
                return Ok ()
            else
                return Error $"HTTP {statusCode}"
        with
        | ex -> return Error $"Exception: {ex.Message}"
    }

/// Create a directory
let createDirectory (path: string) (username: string) (password: string) : JS.Promise<Result<unit, string>> =
    promise {
        try
            let url = $"{workerBaseUrl}/api/mkdir/{JS.encodeURIComponent path}"

            let options = createObj [
                "method" ==> "POST"
                "headers" ==> createObj [
                    "Authorization" ==> getAuthHeader username password
                ]
            ]

            let! response = fetchRaw url options
            let statusCode: int = response?status

            if statusCode >= 200 && statusCode < 300 then
                return Ok ()
            else
                return Error $"HTTP {statusCode}"
        with
        | ex -> return Error $"Exception: {ex.Message}"
    }

/// Download a file
let downloadFile (path: string) (filename: string) (username: string) (password: string) : JS.Promise<Result<unit, string>> =
    promise {
        try
            let url = $"{workerBaseUrl}/api/download?path={JS.encodeURIComponent path}"

            let options = createObj [
                "method" ==> "GET"
                "headers" ==> createObj [
                    "Authorization" ==> getAuthHeader username password
                ]
            ]

            let! response = fetchRaw url options
            let statusCode: int = response?status

            if statusCode = 200 then
                let! blob = response?blob()

                // Create download link
                let blobUrl: string = emitJsExpr blob "URL.createObjectURL($0)"
                let link = Browser.Dom.document.createElement("a") :?> Browser.Types.HTMLAnchorElement
                link.href <- blobUrl
                link?download <- filename
                Browser.Dom.document.body.appendChild(link) |> ignore
                link.click()
                Browser.Dom.document.body.removeChild(link) |> ignore
                emitJsStatement blobUrl "URL.revokeObjectURL($0)"

                return Ok ()
            else
                return Error $"HTTP {statusCode}"
        with
        | ex -> return Error $"Exception: {ex.Message}"
    }

/// Verify credentials by attempting to list root directory
let verifyCredentials (username: string) (password: string) : JS.Promise<Result<unit, string>> =
    promise {
        try
            let url = $"{workerBaseUrl}/api/list?path="

            let options = createObj [
                "method" ==> "GET"
                "headers" ==> createObj [
                    "Authorization" ==> getAuthHeader username password
                ]
            ]

            let! response = fetchRaw url options
            let statusCode: int = response?status

            if statusCode = 200 then
                return Ok ()
            elif statusCode = 401 then
                return Error "Invalid username or password"
            else
                return Error $"HTTP {statusCode}"
        with
        | ex -> return Error $"Exception: {ex.Message}"
    }
