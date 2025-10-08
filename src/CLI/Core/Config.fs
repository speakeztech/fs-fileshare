module FS.FileShare.CLI.Config

open System
open System.IO

type CloudflareConfig = {
    AccountId: string
    ApiToken: string
    R2BucketName: string
    WorkerName: string
    WorkerSubdomain: string
    PagesProjectName: string
}

let loadConfig () : Result<CloudflareConfig, string> =
    let accountId = System.Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID")
    let apiToken = System.Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN")

    // Base names from environment or defaults
    let bucketName = System.Environment.GetEnvironmentVariable("R2_BUCKET_NAME") |> Option.ofObj |> Option.defaultValue "FILESHARE_BUCKET"
    let workerName = System.Environment.GetEnvironmentVariable("WORKER_NAME") |> Option.ofObj |> Option.defaultValue "fs-fileshare-worker"
    let workerSubdomain = System.Environment.GetEnvironmentVariable("WORKER_SUBDOMAIN") |> Option.ofObj |> Option.defaultValue "engineering-0c5"
    let pagesProjectName = System.Environment.GetEnvironmentVariable("PAGES_PROJECT_NAME") |> Option.ofObj |> Option.defaultValue "fs-fileshare"

    match accountId, apiToken with
    | null, _ | "", _ -> Error "CLOUDFLARE_ACCOUNT_ID environment variable not set"
    | _, null | _, "" -> Error "CLOUDFLARE_API_TOKEN environment variable not set"
    | aid, token ->
        Ok {
            AccountId = aid
            ApiToken = token
            R2BucketName = bucketName
            WorkerName = workerName
            WorkerSubdomain = workerSubdomain
            PagesProjectName = pagesProjectName
        }

let getProjectRoot () =
    let rec findRoot (dir: string) =
        if File.Exists(Path.Combine(dir, "package.json")) then
            Some dir
        else
            let parent = Directory.GetParent(dir)
            if isNull parent then None
            else findRoot parent.FullName

    let currentDir = Directory.GetCurrentDirectory()
    match findRoot currentDir with
    | Some root -> root
    | None -> currentDir
