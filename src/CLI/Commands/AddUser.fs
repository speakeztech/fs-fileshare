module FS.FileShare.CLI.Commands.AddUser

open System
open System.Net.Http
open System.Text.Json
open FS.FileShare.CLI.Config
open Spectre.Console

/// Add a user by creating a Cloudflare Secret for their password
let execute (config: CloudflareConfig) (username: string) (password: string) : Async<Result<unit, string>> =
    async {
        AnsiConsole.MarkupLine($"[blue]Adding user:[/] {username}")

        let httpClient = new HttpClient()
        httpClient.BaseAddress <- Uri("https://api.cloudflare.com/client/v4")
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiToken}")

        // Create secret name
        let secretName = $"USER_{username.ToUpper()}_PASSWORD"

        // Upload secret
        let secretUrl = $"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/workers/scripts/{config.WorkerName}/secrets"
        let secretBody = JsonSerializer.Serialize({| name = secretName; text = password; ``type`` = "secret_text" |})

        use content = new StringContent(secretBody, Text.Encoding.UTF8, "application/json")

        let! response =
            AnsiConsole.Status()
                .StartAsync("Creating user secret...", fun ctx ->
                    httpClient.PutAsync(secretUrl, content) |> Async.AwaitTask |> Async.StartAsTask)
                |> Async.AwaitTask

        let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

        use jsonDoc = JsonDocument.Parse(responseBody)
        let json = jsonDoc.RootElement

        let mutable successProp = Unchecked.defaultof<JsonElement>
        if json.TryGetProperty("success", &successProp) && successProp.GetBoolean() then
            AnsiConsole.MarkupLine($"[green]✓[/] User '{username}' added successfully!")
            AnsiConsole.MarkupLine($"[dim]Secret name:[/] {secretName}")
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
                return Error responseBody
    }
