module FS.FileShare.Worker.Main

open Fable.Core
open Fable.Core.JsInterop
open CloudFlare.Worker.Context
open CloudFlare.Worker.Context.Globals
open CloudFlare.Worker.Context.Helpers
open FS.FileShare.Worker.Auth
open FS.FileShare.Worker.WebDav
open FS.FileShare.Worker.FileApi

[<Emit("new URL($0)")>]
let createURL(url: string) : obj = jsNative

/// Add CORS headers to response
let addCorsHeaders (request: Request) (response: Response) : Response =
    // Get the origin from the request
    let origin = request.headers.get("Origin")

    // List of allowed origins
    let allowedOrigins = [
        "https://fs-fs.speakez.tech"
        "https://fs-fileshare.pages.dev"
        "http://localhost:5173"
        "http://localhost:4173"
    ]

    // Set CORS headers based on origin
    match origin with
    | Some org when allowedOrigins |> List.exists (fun allowed -> org = allowed) ->
        response.headers.set("Access-Control-Allow-Origin", org)
        response.headers.set("Access-Control-Allow-Credentials", "true")
    | _ ->
        // Fallback to wildcard for other origins (but without credentials)
        response.headers.set("Access-Control-Allow-Origin", "*")
        response.headers.set("Access-Control-Allow-Credentials", "false")

    response.headers.set("Access-Control-Allow-Methods", "GET, HEAD, PUT, POST, DELETE, OPTIONS, PROPFIND, MKCOL, COPY, MOVE")
    response.headers.set("Access-Control-Allow-Headers", "authorization, content-type, depth, overwrite, destination, range")
    response.headers.set("Access-Control-Expose-Headers", "content-type, content-length, dav, etag, last-modified, location, date, content-range")
    response.headers.set("Access-Control-Max-Age", "86400")
    response

/// Main fetch handler
let fetch (request: Request) (env: Env) (ctx: ExecutionContext) : JS.Promise<Response> =
    promise {
        try
        let url = Request.getUrl request
        let urlObj: obj = createURL(url)
        let pathname: string = urlObj?pathname
        let method = Request.getMethod request

        // CORS preflight doesn't need auth
        if method = "OPTIONS" then
            let response = WebDav.handleOptions ()
            return addCorsHeaders request response

        // Check if request is for WebDAV API
        elif pathname.StartsWith(WebDav.API_PREFIX) then
            // Get authorization header
            let authHeader = request.headers.get("Authorization")

            // Parse and verify auth
            let! authResult = parseAndVerifyAuth authHeader env

            match authResult with
            | None ->
                let response = unauthorizedResponse ()
                return addCorsHeaders request response
            | Some (username, bucket) ->
                // Process WebDAV request
                let! response = WebDav.dispatchHandler request bucket
                return addCorsHeaders request response

        // Check if request is for File API
        elif pathname.StartsWith(FileApi.API_PREFIX) then
            // Get authorization header
            let authHeader = request.headers.get("Authorization")

            // Parse and verify auth
            let! authResult = parseAndVerifyAuth authHeader env

            match authResult with
            | None ->
                let response = unauthorizedResponse ()
                return addCorsHeaders request response
            | Some (username, bucket) ->
                // Process API request
                let! response = FileApi.dispatchApiHandler request bucket
                return addCorsHeaders request response

        // Default: Return info page or serve static files (will be handled by Pages)
        else
            let html = """
<!DOCTYPE html>
<html style="background: #000;">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>FS FileShare</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        html, body { background: #000; color: #e0e0e0; }
        body { font-family: system-ui, -apple-system, sans-serif; max-width: 800px; margin: 50px auto; padding: 0 20px; line-height: 1.6; }
        h1 { color: #f38020; margin-bottom: 10px; }
        h2 { color: #e0e0e0; margin-top: 30px; margin-bottom: 15px; }
        p { margin: 10px 0; }
        code { background: #1a1a1a; color: #f38020; padding: 2px 8px; border-radius: 4px; border: 1px solid #333; }
        .info { background: #0d1b2a; padding: 15px; border-radius: 5px; border-left: 4px solid #2196F3; margin: 20px 0; }
        ul { margin: 15px 0; padding-left: 25px; }
        li { margin: 8px 0; }
        a { color: #2196F3; text-decoration: none; }
        a:hover { text-decoration: underline; }
    </style>
</head>
<body>
    <h1>🚀 FS FileShare</h1>
    <p>CloudFlare Workers-based file sharing system with WebDAV and Web UI support.</p>

    <div class="info">
        <strong>⚠️ Endpoints:</strong><br>
        WebDAV: <code>/webdav</code><br>
        File API: <code>/api</code><br>
        Web UI: <code>/</code> (via Cloudflare Pages)
    </div>

    <h2>Features</h2>
    <ul>
        <li>Shared R2 bucket for all users</li>
        <li>Basic authentication</li>
        <li>Full WebDAV protocol support</li>
        <li>RESTful File API for web interface</li>
        <li>Modern web UI with drag-and-drop</li>
        <li>CORS enabled</li>
    </ul>
</body>
</html>"""
            let headers = Headers.Create()
            headers.set("Content-Type", "text/html; charset=utf-8")
            let init = createObj [
                "status" ==> 200
                "headers" ==> headers
            ]
            return Response.Create(U2.Case1 html, unbox init)
        with ex ->
            // Handle any errors and return 500 with CORS headers
            let errorHtml = $"Internal Server Error: {ex.Message}"
            let headers = Headers.Create()
            headers.set("Content-Type", "text/plain")
            let init = createObj [
                "status" ==> 500
                "headers" ==> headers
            ]
            let response = Response.Create(U2.Case1 errorHtml, unbox init)
            return addCorsHeaders request response
    }

/// Export the handler
[<ExportDefault>]
let handler: obj = {| fetch = fetch |} :> obj
