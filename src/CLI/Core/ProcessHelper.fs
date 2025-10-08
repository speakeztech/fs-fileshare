module FS.FileShare.CLI.ProcessHelper

open System
open System.Diagnostics
open System.IO
open Spectre.Console

/// Run a process and return success/failure with output
let runProcess (executable: string) (arguments: string) (workingDir: string) : Async<Result<string, string>> =
    async {
        try
            let psi = ProcessStartInfo()

            // On Windows, npm and wrangler are .cmd files, so we need to run through cmd.exe
            if Environment.OSVersion.Platform = PlatformID.Win32NT &&
               (executable = "npm" || executable = "wrangler") then
                psi.FileName <- "cmd.exe"
                psi.Arguments <- $"/c {executable} {arguments}"
            else
                psi.FileName <- executable
                psi.Arguments <- arguments

            psi.WorkingDirectory <- workingDir
            psi.RedirectStandardOutput <- true
            psi.RedirectStandardError <- true
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- true

            // Copy all environment variables to the child process
            let envVars = Environment.GetEnvironmentVariables()
            for entry in envVars |> Seq.cast<System.Collections.DictionaryEntry> do
                let keyStr = entry.Key.ToString()
                let value = entry.Value.ToString()
                if not (psi.Environment.ContainsKey(keyStr)) then
                    psi.Environment.[keyStr] <- value

            use proc = Process.Start(psi)

            let! stdout = proc.StandardOutput.ReadToEndAsync() |> Async.AwaitTask
            let! stderr = proc.StandardError.ReadToEndAsync() |> Async.AwaitTask
            let! _ = proc.WaitForExitAsync() |> Async.AwaitTask

            if proc.ExitCode = 0 then
                return Ok stdout
            else
                return Error (if String.IsNullOrWhiteSpace stderr then stdout else stderr)
        with
        | ex -> return Error $"Exception: {ex.Message}"
    }

/// Run npm command
let runNpm (command: string) (workingDir: string) : Async<Result<unit, string>> =
    async {
        let! result = runProcess "npm" command workingDir
        match result with
        | Ok _ -> return Ok ()
        | Error err -> return Error err
    }

/// Run wrangler command
let runWrangler (command: string) (workingDir: string) : Async<Result<string, string>> =
    runProcess "wrangler" command workingDir

/// Run fable command
let runFable (command: string) (workingDir: string) : Async<Result<string, string>> =
    runProcess "dotnet" $"fable {command}" workingDir

/// Display process output with Spectre
let runProcessWithProgress (title: string) (executable: string) (arguments: string) (workingDir: string) : Async<Result<unit, string>> =
    AnsiConsole.Status()
        .StartAsync(title, fun ctx ->
            async {
                let! result = runProcess executable arguments workingDir
                match result with
                | Ok output ->
                    if not (String.IsNullOrWhiteSpace output) then
                        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(output.Substring(0, min 200 output.Length))}...[/]")
                    AnsiConsole.MarkupLine("[green]✓[/] Success")
                    return Ok ()
                | Error err ->
                    let escaped = Markup.Escape(err)
                    AnsiConsole.MarkupLine($"[red]✗[/] Failed: {escaped}")
                    return Error err
            } |> Async.StartAsTask)
        |> Async.AwaitTask
