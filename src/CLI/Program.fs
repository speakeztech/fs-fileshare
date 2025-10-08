module FS.FileShare.CLI.Program

open Argu
open Spectre.Console
open FS.FileShare.CLI.Config

type DeployArgs =
    | [<Unique>] Skip_Build
    | [<AltCommandLine("-f")>] Force
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Skip_Build -> "Skip the build step and deploy existing artifacts"
            | Force -> "Force redeployment even if source hasn't changed"

type AddUserArgs =
    | [<Mandatory; Unique>] Username of string
    | [<Mandatory; Unique>] Password of string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Username _ -> "Username for the new user"
            | Password _ -> "Password for the new user"

type CLIArgs =
    | [<CliPrefix(CliPrefix.None)>] Deploy of ParseResults<DeployArgs>
    | [<CliPrefix(CliPrefix.None)>] Add_User of ParseResults<AddUserArgs>
    | [<CliPrefix(CliPrefix.None)>] Status
    | [<Unique>] Version
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Deploy _ -> "Deploy Worker and Pages to Cloudflare"
            | Add_User _ -> "Add a new user with credentials"
            | Status -> "Show deployment status"
            | Version -> "Show version information"

[<EntryPoint>]
let main argv =
    try
        let parser = ArgumentParser.Create<CLIArgs>(programName = "fs-cli")

        if argv.Length = 0 then
            AnsiConsole.Write(
                FigletText("FS Fileshare")
                    .Centered()
                    .Color(Color.Cyan1))
            AnsiConsole.WriteLine()
            AnsiConsole.MarkupLine("[dim]Cloudflare Pages + Worker File Sharing System[/]")
            AnsiConsole.WriteLine()
            AnsiConsole.WriteLine(parser.PrintUsage())
            0
        else
            let results = parser.ParseCommandLine(argv)

            if results.Contains Version then
                AnsiConsole.MarkupLine("FS Fileshare CLI [cyan]v1.0.0[/]")
                0
            else
                // Load config
                match loadConfig() with
                | Error err ->
                    AnsiConsole.MarkupLine($"[red]Configuration error:[/] {err}")
                    AnsiConsole.WriteLine()
                    AnsiConsole.MarkupLine("Required environment variables:")
                    AnsiConsole.MarkupLine("  [yellow]CLOUDFLARE_ACCOUNT_ID[/]")
                    AnsiConsole.MarkupLine("  [yellow]CLOUDFLARE_API_TOKEN[/]")
                    AnsiConsole.WriteLine()
                    AnsiConsole.MarkupLine("Optional environment variables:")
                    AnsiConsole.MarkupLine("  [dim]R2_BUCKET_NAME[/] (default: fs-fileshare)")
                    AnsiConsole.MarkupLine("  [dim]WORKER_NAME[/] (default: fs-fileshare-worker)")
                    AnsiConsole.MarkupLine("  [dim]PAGES_PROJECT_NAME[/] (default: fs-fileshare)")
                    1
                | Ok config ->
                    let runAsync f =
                        async {
                            let! result = f
                            match result with
                            | Ok () -> return 0
                            | Error err ->
                                let escaped = Markup.Escape(err)
                                AnsiConsole.MarkupLine($"[red]Error:[/] {escaped}")
                                return 1
                        } |> Async.RunSynchronously

                    if results.Contains Deploy then
                        let deployArgs = results.GetResult Deploy
                        let skipBuild = deployArgs.Contains Skip_Build
                        let force = deployArgs.Contains Force
                        runAsync (Commands.Deploy.execute config skipBuild force)

                    elif results.Contains Add_User then
                        let addUserArgs = results.GetResult Add_User
                        let username = addUserArgs.GetResult Username
                        let password = addUserArgs.GetResult Password
                        runAsync (Commands.AddUser.execute config username password)

                    elif results.Contains Status then
                        runAsync (Commands.Status.execute config)

                    else
                        AnsiConsole.WriteLine(parser.PrintUsage())
                        0

    with
    | :? ArguParseException as ex ->
        let escaped = Markup.Escape(ex.Message)
        AnsiConsole.MarkupLine($"[red]{escaped}[/]")
        1
    | ex ->
        let escaped = Markup.Escape(ex.Message)
        AnsiConsole.MarkupLine($"[red]Unexpected error:[/] {escaped}")
        1
