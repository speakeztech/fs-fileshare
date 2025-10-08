module FS.FileShare.CLI.Commands.Deploy

open System
open System.IO
open System.Net.Http
open System.Text.Json
open FS.FileShare.CLI.Config
open FS.FileShare.CLI.R2Client
open FS.FileShare.CLI.ProcessHelper
open Spectre.Console
open CloudFlare.Management.Workers

/// Deploy the Worker to Cloudflare
let deployWorker (config: CloudflareConfig) (projectRoot: string) (force: bool) : Async<Result<unit, string>> =
    async {
        let workerDir = Path.Combine(projectRoot, "src", "Worker")
        let distPath = Path.Combine(projectRoot, "dist", "worker")

        if not (Directory.Exists(workerDir)) then
            return Error $"Worker directory not found: {workerDir}"
        else

        AnsiConsole.MarkupLine($"[blue]Deploying Worker:[/] {config.WorkerName}")
        AnsiConsole.MarkupLine($"[dim]Source:[/] {workerDir}")

        // Step 1: Check if source has changed since last deploy
        let stateFilePath = Path.Combine(workerDir, ".deploy-state")
        let sourceFiles = Directory.GetFiles(workerDir, "*.fs", SearchOption.AllDirectories)

        // Compute hash of all source files
        let computeSourceHash () =
            sourceFiles
            |> Array.map File.ReadAllText
            |> String.concat ""
            |> fun s ->
                use sha = System.Security.Cryptography.SHA256.Create()
                s |> Text.Encoding.UTF8.GetBytes |> sha.ComputeHash |> Convert.ToHexString

        let currentHash = computeSourceHash()

        // Check if we can skip deployment (unless force is specified)
        let shouldDeploy =
            if force then
                true
            elif File.Exists(stateFilePath) then
                let lastState = File.ReadAllText(stateFilePath)
                let lastHash =
                    if lastState.Contains("|") then lastState.Split('|').[0]
                    else ""
                lastHash <> currentHash
            else
                true

        if not shouldDeploy then
            AnsiConsole.MarkupLine("[yellow]⊘[/] No changes detected since last deployment. Skipping.")
            AnsiConsole.MarkupLine("[dim]Use --force to deploy anyway[/]")
            return Ok ()
        else

        if force then
            AnsiConsole.MarkupLine("[yellow]⚡[/] Force deployment requested")
            AnsiConsole.WriteLine()

        // Step 2: Compile with Fable
        let! fableResult =
            runProcessWithProgress
                "Compiling Worker with Fable..."
                "dotnet"
                $"fable {workerDir} --outDir {distPath}"
                projectRoot

        match fableResult with
        | Error err -> return Error err
        | Ok () ->

        // Step 3: Find entry point
        let fsprojFiles = Directory.GetFiles(workerDir, "*.fsproj")
        if fsprojFiles.Length = 0 then
            return Error $"No .fsproj file found in {workerDir}"
        else

        let fsprojPath = fsprojFiles.[0]
        let fsprojContent = File.ReadAllText(fsprojPath)
        let compileIncludes =
            System.Text.RegularExpressions.Regex.Matches(fsprojContent, "<Compile Include=\"([^\"]+)\" />")
            |> Seq.cast<System.Text.RegularExpressions.Match>
            |> Seq.map (fun m -> m.Groups.[1].Value)
            |> Seq.toList

        if compileIncludes.IsEmpty then
            return Error $"No Compile Include entries found in {fsprojPath}"
        else

        let entryPointFile = compileIncludes |> List.last
        let entryPointName = Path.GetFileNameWithoutExtension(entryPointFile)
        let mainJsPath = Path.Combine(distPath, $"{entryPointName}.js")

        if not (File.Exists(mainJsPath)) then
            return Error $"Compiled worker not found: {mainJsPath}"
        else

        AnsiConsole.MarkupLine($"[dim]Entry point:[/] {entryPointName}.js")

        // Step 4: Upload to Cloudflare using Workers API
        let httpClient = new HttpClient()
        httpClient.BaseAddress <- Uri("https://api.cloudflare.com/client/v4")
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiToken}")

        // Get all JS files
        let jsFiles = Directory.GetFiles(distPath, "*.js", SearchOption.AllDirectories)

        // Create metadata
        let metadata =
            JsonSerializer.Serialize(
                {|
                    main_module = $"{entryPointName}.js"
                    compatibility_date = "2024-01-01"
                    compatibility_flags = [| "nodejs_compat" |]
                    bindings = [|
                        {|
                            ``type`` = "r2_bucket"
                            name = "FILESHARE_BUCKET"
                            bucket_name = config.R2BucketName
                        |}
                    |]
                |},
                JsonSerializerOptions(WriteIndented = false)
            )

        // Upload worker
        use formData = new MultipartFormDataContent()
        formData.Add(new StringContent(metadata), "metadata")

        for jsFile in jsFiles do
            let relativePath = Path.GetRelativePath(distPath, jsFile).Replace("\\", "/")
            let scriptBytes = File.ReadAllBytes(jsFile)
            let scriptContent = new ByteArrayContent(scriptBytes)
            scriptContent.Headers.ContentType <- Headers.MediaTypeHeaderValue("application/javascript+module")
            formData.Add(scriptContent, relativePath, relativePath)

        let uploadUrl = $"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/workers/scripts/{config.WorkerName}"

        let! (response, content) =
            AnsiConsole.Status()
                .StartAsync("Uploading Worker to Cloudflare...", fun ctx ->
                    async {
                        let! resp = httpClient.PutAsync(uploadUrl, formData) |> Async.AwaitTask
                        let! cont = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                        return (resp, cont)
                    } |> Async.StartAsTask)
                |> Async.AwaitTask

        use jsonDoc = JsonDocument.Parse(content)
        let json = jsonDoc.RootElement

        let mutable successProp = Unchecked.defaultof<JsonElement>
        if json.TryGetProperty("success", &successProp) && successProp.GetBoolean() then
            // Save deployment state for idempotency
            File.WriteAllText(stateFilePath, $"{currentHash}|{DateTime.UtcNow:O}")
            AnsiConsole.MarkupLine("[green]✓[/] Worker deployed successfully!")

            // Try to enable workers.dev subdomain
            let enableUrl = $"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/workers/scripts/{config.WorkerName}/subdomain"
            let enableBody = """{"enabled":true}"""
            use enableContent = new StringContent(enableBody, Text.Encoding.UTF8, "application/json")
            let! enableResponse = httpClient.PostAsync(enableUrl, enableContent) |> Async.AwaitTask

            AnsiConsole.MarkupLine($"[cyan]Worker URL:[/] https://{config.WorkerName}.{config.AccountId}.workers.dev")
            return Ok ()
        else
            let mutable errorsProp = Unchecked.defaultof<JsonElement>
            if json.TryGetProperty("errors", &errorsProp) && errorsProp.ValueKind = JsonValueKind.Array then
                let errors =
                    errorsProp.EnumerateArray()
                    |> Seq.map (fun e ->
                        let mutable msg = Unchecked.defaultof<JsonElement>
                        if e.TryGetProperty("message", &msg) then msg.GetString() else "Unknown error")
                    |> String.concat("\n")
                return Error errors
            else
                return Error content
    }

/// Check if Pages project exists via API
let checkPagesProjectExists (config: CloudflareConfig) : Async<bool> =
    async {
        try
            use httpClient = new HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiToken}")

            let url = $"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/pages/projects/{config.PagesProjectName}"
            let! response = httpClient.GetAsync(url) |> Async.AwaitTask

            return response.IsSuccessStatusCode
        with
        | _ -> return false
    }

/// Create Pages project via API
let createPagesProject (config: CloudflareConfig) : Async<Result<unit, string>> =
    async {
        try
            use httpClient = new HttpClient()
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiToken}")

            let url = $"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/pages/projects"
            let payload = $"""{{
                "name": "{config.PagesProjectName}",
                "production_branch": "main",
                "deployment_configs": {{
                    "production": {{}},
                    "preview": {{}}
                }}
            }}"""

            use content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            let! response = httpClient.PostAsync(url, content) |> Async.AwaitTask
            let! responseText = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            if response.IsSuccessStatusCode then
                return Ok ()
            else
                return Error $"Failed to create Pages project: {responseText}"
        with ex ->
            return Error $"Exception creating Pages project: {ex.Message}"
    }

/// Deploy Pages using Wrangler
let deployPages (config: CloudflareConfig) (projectRoot: string) : Async<Result<unit, string>> =
    async {
        let distPath = Path.Combine(projectRoot, "dist", "pages")

        if not (Directory.Exists(distPath)) then
            return Error $"Pages dist directory not found: {distPath}. Run 'npm run build:pages' first."
        else

        AnsiConsole.MarkupLine($"[blue]Deploying Pages:[/] {config.PagesProjectName}")
        AnsiConsole.MarkupLine($"[dim]Source:[/] {distPath}")

        // Check if project exists, create if not
        let! projectExists = checkPagesProjectExists config
        let! createOk =
            async {
                if not projectExists then
                    AnsiConsole.MarkupLine($"[yellow]Creating Pages project:[/] {config.PagesProjectName}")
                    let! createResult = createPagesProject config
                    match createResult with
                    | Error err -> return Error err
                    | Ok () ->
                        AnsiConsole.MarkupLine($"[green]✓[/] Pages project created")
                        AnsiConsole.WriteLine()
                        return Ok ()
                else
                    return Ok ()
            }

        match createOk with
        | Error err -> return Error err
        | Ok () ->

            // Deploy using Wrangler with --commit-dirty to avoid interactive prompts
            let! result =
                runProcessWithProgress
                    "Deploying to Cloudflare Pages..."
                    "wrangler"
                    $"pages deploy \"{distPath}\" --project-name={config.PagesProjectName} --commit-dirty=true"
                    projectRoot

            match result with
            | Ok () ->
                AnsiConsole.MarkupLine("[green]✓[/] Pages deployed successfully!")
                AnsiConsole.MarkupLine($"[cyan]Pages URL:[/] https://{config.PagesProjectName}.pages.dev")
                return Ok ()
            | Error err ->
                return Error err
    }

/// Create R2 bucket if it doesn't exist
let ensureR2Bucket (config: CloudflareConfig) : Async<Result<unit, string>> =
    AnsiConsole.Status()
        .StartAsync("Checking/creating R2 bucket...", fun ctx ->
            async {
                let r2 = R2Operations(config)

                // Check if bucket already exists
                let! existingBuckets = r2.ListBuckets()
                match existingBuckets with
                | Ok buckets when buckets |> List.exists (fun b -> b.Name = config.R2BucketName) ->
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] Bucket [cyan]{config.R2BucketName}[/] already exists, skipping creation")
                    return Ok ()
                | Ok _ ->
                    // Bucket doesn't exist, create it
                    let! result = r2.CreateBucket(config.R2BucketName)
                    match result with
                    | Ok () ->
                        AnsiConsole.MarkupLine($"[green]✓[/] Created bucket: [cyan]{config.R2BucketName}[/]")
                        return Ok ()
                    | Error err ->
                        let escaped = Markup.Escape(err)
                        AnsiConsole.MarkupLine($"[red]✗[/] Failed to create bucket: {escaped}")
                        return Error err
                | Error err ->
                    let escaped = Markup.Escape(err)
                    AnsiConsole.MarkupLine($"[red]✗[/] Failed to list buckets: {escaped}")
                    return Error err
            } |> Async.StartAsTask)
        |> Async.AwaitTask

/// Full deployment: build everything and deploy
let execute (config: CloudflareConfig) (skipBuild: bool) (force: bool) : Async<Result<unit, string>> =
    async {
        let projectRoot = getProjectRoot()

        let rule = Rule("[yellow]FS FileShare Deployment[/]")
        rule.Justification <- Justify.Left
        AnsiConsole.Write(rule)
        AnsiConsole.WriteLine()

        // Step 0: Ensure R2 bucket exists
        let! bucketResult = ensureR2Bucket config
        match bucketResult with
        | Error err -> return Error $"R2 bucket setup failed: {err}"
        | Ok () ->
            AnsiConsole.WriteLine()

            // Step 1: Build if not skipped
            if not skipBuild then
                AnsiConsole.MarkupLine("[blue]Building project...[/]")

                // Set the Worker URL for the Pages build
                let workerUrl = $"https://{config.WorkerName}.{config.WorkerSubdomain}.workers.dev"
                Environment.SetEnvironmentVariable("VITE_WORKER_URL", workerUrl)
                AnsiConsole.MarkupLine($"[dim]Worker URL:[/] {workerUrl}")

                let! buildResult = runProcessWithProgress "Running npm run build..." "npm" "run build" projectRoot

                match buildResult with
                | Error err -> return Error $"Build failed: {err}"
                | Ok () ->
                    AnsiConsole.WriteLine()

                    // Step 2: Deploy Worker
                    let! workerResult = deployWorker config projectRoot force
                    match workerResult with
                    | Error err -> return Error $"Worker deployment failed: {err}"
                    | Ok () ->
                        AnsiConsole.WriteLine()

                        // Step 3: Deploy Pages
                        let! pagesResult = deployPages config projectRoot
                        match pagesResult with
                        | Error err -> return Error $"Pages deployment failed: {err}"
                        | Ok () ->
                            AnsiConsole.WriteLine()
                            AnsiConsole.MarkupLine("[green bold]✓ Deployment complete![/]")
                            return Ok ()
            else
                // Step 2: Deploy Worker (skip build)
                let! workerResult = deployWorker config projectRoot force
                match workerResult with
                | Error err -> return Error $"Worker deployment failed: {err}"
                | Ok () ->
                    AnsiConsole.WriteLine()

                    // Step 3: Deploy Pages
                    let! pagesResult = deployPages config projectRoot
                    match pagesResult with
                    | Error err -> return Error $"Pages deployment failed: {err}"
                    | Ok () ->
                        AnsiConsole.WriteLine()
                        AnsiConsole.MarkupLine("[green bold]✓ Deployment complete![/]")
                        return Ok ()
    }
