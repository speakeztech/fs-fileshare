module FS.FileShare.CLI.Commands.Status

open System
open System.Net.Http
open System.Text.Json
open FS.FileShare.CLI.Config
open Spectre.Console

/// Show deployment status
let execute (config: CloudflareConfig) : Async<Result<unit, string>> =
    async {
        let httpClient = new HttpClient()
        httpClient.BaseAddress <- Uri("https://api.cloudflare.com/client/v4")
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiToken}")

        let rule = Rule("[yellow]Deployment Status[/]")
        rule.Justification <- Justify.Left
        AnsiConsole.Write(rule)
        AnsiConsole.WriteLine()

        // Check Worker
        let workerUrl = $"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/workers/scripts/{config.WorkerName}"
        let! workerResponse = httpClient.GetAsync(workerUrl) |> Async.AwaitTask
        let! workerContent = workerResponse.Content.ReadAsStringAsync() |> Async.AwaitTask

        if workerResponse.IsSuccessStatusCode then
            AnsiConsole.MarkupLine($"[green]✓[/] Worker: [cyan]{config.WorkerName}[/]")
            AnsiConsole.MarkupLine($"  URL: https://{config.WorkerName}.{config.AccountId}.workers.dev")
        else
            AnsiConsole.MarkupLine($"[red]✗[/] Worker: Not deployed")

        AnsiConsole.WriteLine()

        // Check Pages
        let pagesUrl = $"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/pages/projects/{config.PagesProjectName}"
        let! pagesResponse = httpClient.GetAsync(pagesUrl) |> Async.AwaitTask
        let! pagesContent = pagesResponse.Content.ReadAsStringAsync() |> Async.AwaitTask

        if pagesResponse.IsSuccessStatusCode then
            use jsonDoc = JsonDocument.Parse(pagesContent)
            let json = jsonDoc.RootElement
            let mutable resultProp = Unchecked.defaultof<JsonElement>

            if json.TryGetProperty("result", &resultProp) then
                let mutable subdomain = Unchecked.defaultof<JsonElement>
                let mutable deploymentId = Unchecked.defaultof<JsonElement>

                AnsiConsole.MarkupLine($"[green]✓[/] Pages: [cyan]{config.PagesProjectName}[/]")

                if resultProp.TryGetProperty("subdomain", &subdomain) then
                    AnsiConsole.MarkupLine($"  URL: https://{subdomain.GetString()}")

                if resultProp.TryGetProperty("latest_deployment", &deploymentId) then
                    let mutable envProp = Unchecked.defaultof<JsonElement>
                    if deploymentId.TryGetProperty("environment", &envProp) then
                        AnsiConsole.MarkupLine($"  Environment: {envProp.GetString()}")
        else
            AnsiConsole.MarkupLine($"[red]✗[/] Pages: Not deployed")

        AnsiConsole.WriteLine()

        // Show R2 Bucket
        AnsiConsole.MarkupLine($"[blue]R2 Bucket:[/] {config.R2BucketName}")

        return Ok ()
    }
