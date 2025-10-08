module FS.FileShare.Pages.Components.FileManager

open Feliz
open Feliz.DaisyUI
open Feliz.UseElmish
open Elmish
open FS.FileShare.Pages.FileApi
open FS.FileShare.Pages.Components.FileItem
open FS.FileShare.Pages.Components.UploadProgressModal
open Browser.Types

type State = {
    CurrentPath: string option
    Files: FileInfo array
    Loading: bool
    Error: string option
    Username: string
    Password: string
    ShowNewFolderDialog: bool
    NewFolderName: string
    UploadingFiles: Map<string, float>
    ShowDeleteConfirmation: bool
    FileToDelete: FileInfo option
}


type Msg =
    | LoadFiles
    | FilesLoaded of Result<FileInfo array, string>
    | NavigateToPath of string option
    | DownloadFile of FileInfo
    | DeleteFile of FileInfo
    | ConfirmDelete
    | CancelDelete
    | UploadFiles of File array
    | FileUploaded of string * Result<unit, string>
    | UpdateUploadProgress of string * float
    | ClearUploadProgress
    | ShowNewFolderDialog
    | HideNewFolderDialog
    | SetNewFolderName of string
    | CreateFolder
    | FolderCreated of Result<unit, string>
    | SetCredentials of string * string
    | DismissError

let init (username: string) (password: string) =
    {
        CurrentPath = None
        Files = [||]
        Loading = false
        Error = None
        Username = username
        Password = password
        ShowNewFolderDialog = false
        NewFolderName = ""
        ShowDeleteConfirmation = false
        FileToDelete = None
        UploadingFiles = Map.empty
    }, Cmd.ofMsg LoadFiles

let update (msg: Msg) (state: State) : State * Cmd<Msg> =
    match msg with
    | LoadFiles ->
        { state with Loading = true; Error = None },
        Cmd.OfPromise.either
            (fun () -> listFiles state.CurrentPath state.Username state.Password)
            ()
            FilesLoaded
            (fun ex -> FilesLoaded (Error ex.Message))

    | FilesLoaded result ->
        match result with
        | Ok files ->
            // Sort: folders first (alphabetically), then files (alphabetically)
            let sortedFiles =
                files
                |> Array.sortBy (fun f -> (not f.IsDirectory, f.Name.ToLower()))
            { state with Files = sortedFiles; Loading = false }, Cmd.none
        | Error err ->
            { state with Error = Some err; Loading = false }, Cmd.none

    | NavigateToPath path ->
        { state with CurrentPath = path }, Cmd.ofMsg LoadFiles

    | DownloadFile file ->
        state,
        Cmd.OfPromise.perform
            (fun () -> downloadFile file.Path file.Name state.Username state.Password)
            ()
            (fun _ -> LoadFiles)
    | DeleteFile file ->
        { state with ShowDeleteConfirmation = true; FileToDelete = Some file }, Cmd.none

    | ConfirmDelete ->
        match state.FileToDelete with
        | Some file ->
            { state with ShowDeleteConfirmation = false; FileToDelete = None },
            Cmd.OfPromise.either
                (fun () -> deleteItem file.Path state.Username state.Password)
                ()
                (fun _ -> LoadFiles)
                (fun ex -> FilesLoaded (Error ex.Message))
        | None ->
            state, Cmd.none

    | CancelDelete ->
        { state with ShowDeleteConfirmation = false; FileToDelete = None }, Cmd.none

    | UploadFiles files ->
        // Initialize upload tracking for all files
        let uploadingFiles =
            files
            |> Array.fold (fun map file -> Map.add file.name 0.0 map) state.UploadingFiles

        let uploadCommands =
            files
            |> Array.map (fun file ->
                let fullPath =
                    match state.CurrentPath with
                    | None -> file.name
                    | Some path -> $"{path}/{file.name}"

                Cmd.OfPromise.either
                    (fun () -> uploadFile fullPath file state.Username state.Password)
                    ()
                    (fun result -> FileUploaded (file.name, result))
                    (fun ex -> FileUploaded (file.name, Error ex.Message))
            )

        { state with UploadingFiles = uploadingFiles }, Cmd.batch (Array.toList uploadCommands)

    | FileUploaded (filename, result) ->
        let uploadingFiles =
            match result with
            | Ok () -> Map.add filename 1.0 state.UploadingFiles
            | Error _ -> Map.add filename -1.0 state.UploadingFiles

        let allComplete =
            uploadingFiles
            |> Map.forall (fun _ progress -> progress = 1.0 || progress = -1.0)

        let hasErrors =
            uploadingFiles
            |> Map.exists (fun _ progress -> progress = -1.0)

        let newState = { state with UploadingFiles = uploadingFiles }

        match result with
        | Error err when not allComplete ->
            { newState with Error = Some err }, Cmd.none
        | Error err ->
            { newState with Error = Some err }, Cmd.ofMsg LoadFiles
        | Ok () when allComplete ->
            newState, Cmd.ofMsg LoadFiles
        | Ok () ->
            newState, Cmd.none

    | UpdateUploadProgress (filename, progress) ->
        let uploadingFiles = Map.add filename progress state.UploadingFiles
        { state with UploadingFiles = uploadingFiles }, Cmd.none

    | ClearUploadProgress ->
        { state with UploadingFiles = Map.empty }, Cmd.none

    | ShowNewFolderDialog ->
        { state with ShowNewFolderDialog = true; NewFolderName = "" }, Cmd.none

    | HideNewFolderDialog ->
        { state with ShowNewFolderDialog = false; NewFolderName = "" }, Cmd.none

    | SetNewFolderName name ->
        { state with NewFolderName = name }, Cmd.none

    | CreateFolder ->
        if state.NewFolderName = "" then
            state, Cmd.none
        else
            let fullPath =
                match state.CurrentPath with
                | None -> state.NewFolderName
                | Some path -> $"{path}/{state.NewFolderName}"

            { state with ShowNewFolderDialog = false },
            Cmd.OfPromise.either
                (fun () -> createDirectory fullPath state.Username state.Password)
                ()
                FolderCreated
                (fun ex -> FolderCreated (Error ex.Message))

    | FolderCreated result ->
        match result with
        | Ok () ->
            state, Cmd.ofMsg LoadFiles
        | Error err ->
            { state with Error = Some err }, Cmd.none

    | SetCredentials (username, password) ->
        { state with Username = username; Password = password }, Cmd.ofMsg LoadFiles

    | DismissError ->
        { state with Error = None }, Cmd.none

[<ReactComponent>]
let FileManager (username: string) (password: string) =
    let state, dispatch = React.useElmish((fun () -> init username password), update, [||])

    let uploadStatuses =
        state.UploadingFiles
        |> Map.toList
        |> List.map (fun (filename, progress) ->
            {
                Filename = filename
                Progress = if progress < 0.0 then 1.0 else progress
                IsComplete = progress = 1.0 || progress = -1.0
                HasError = progress = -1.0
            }
        )

    Html.div [
        prop.className "flex flex-col h-full gap-4"
        prop.children [
            // Toolbar
            Html.div [
                prop.className "flex justify-between items-center"
                prop.children [
                    FS.FileShare.Pages.Components.Breadcrumb.Breadcrumb state.CurrentPath (NavigateToPath >> dispatch)

                    Html.div [
                        prop.className "flex gap-2"
                        prop.children [
                            Daisy.button.button [
                                button.primary
                                prop.onClick (fun _ -> dispatch ShowNewFolderDialog)
                                prop.children [
                                    Html.i [ prop.className "fa-solid fa-folder-plus mr-2" ]
                                    Html.span "New Folder"
                                ]
                            ]

                            Daisy.button.button [
                                button.ghost
                                prop.onClick (fun _ -> dispatch LoadFiles)
                                prop.children [
                                    Html.i [ prop.className "fa-solid fa-rotate-right" ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            // Upload Zone
            FS.FileShare.Pages.Components.UploadZone.UploadZone (UploadFiles >> dispatch)

            // Error display
            match state.Error with
            | Some err ->
                Daisy.alert [
                    alert.error
                    prop.className "relative"
                    prop.children [
                        Html.span err
                        Daisy.button.button [
                            button.ghost
                            button.sm
                            button.circle
                            prop.className "absolute right-2 top-2"
                            prop.onClick (fun _ -> dispatch DismissError)
                            prop.children [
                                Html.i [ prop.className "fa-solid fa-xmark" ]
                            ]
                        ]
                    ]
                ]
            | None -> Html.none

            // Loading indicator
            if state.Loading then
                Html.div [
                    prop.className "flex justify-center p-8"
                    prop.children [
                        Daisy.loading [ loading.spinner; loading.lg ]
                    ]
                ]
            else
                // File list
                Html.div [
                    prop.className "overflow-x-auto"
                    prop.children [
                        Html.table [
                            prop.className "table table-zebra"
                            prop.children [
                                Html.thead [
                                    Html.tr [
                                        Html.th "Name"
                                        Html.th "Size"
                                        Html.th "Last Modified"
                                        Html.th "Actions"
                                    ]
                                ]

                                Html.tbody [
                                    for file in state.Files do
                                        FileItem file (fun action ->
                                            match action with
                                            | FileItemMsg.Navigate ->
                                                dispatch (NavigateToPath (Some file.Path))
                                            | FileItemMsg.Download ->
                                                dispatch (DownloadFile file)
                                            | FileItemMsg.Delete ->
                                                dispatch (DeleteFile file)
                                        )
                                ]
                            ]
                        ]
                    ]
                ]

            // New Folder Dialog
            if state.ShowNewFolderDialog then
                Html.div [
                    prop.className "modal modal-open"
                    prop.children [
                        Html.div [
                            prop.className "modal-box"
                            prop.children [
                                Html.h3 [
                                    prop.className "font-bold text-lg"
                                    prop.text "Create New Folder"
                                ]

                                Html.div [
                                    prop.className "py-4"
                                    prop.children [
                                        Daisy.formControl [
                                            Daisy.label [
                                                Daisy.labelText "Folder Name"
                                            ]
                                            Daisy.input [
                                                input.bordered
                                                prop.value state.NewFolderName
                                                prop.onChange (SetNewFolderName >> dispatch)
                                                prop.placeholder "Enter folder name"
                                            ]
                                        ]
                                    ]
                                ]

                                Html.div [
                                    prop.className "modal-action"
                                    prop.children [
                                        Daisy.button.button [
                                            button.ghost
                                            prop.onClick (fun _ -> dispatch HideNewFolderDialog)
                                            prop.text "Cancel"
                                        ]

                                        Daisy.button.button [
                                            button.primary
                                            prop.onClick (fun _ -> dispatch CreateFolder)
                                            prop.text "Create"
                                        ]
                                ]
                                ]
                            ]
                        ]
                    ]
                ]

            // Delete Confirmation Modal
            if state.ShowDeleteConfirmation then
                match state.FileToDelete with
                | Some file ->
                    Html.div [
                        prop.className "modal modal-open"
                        prop.children [
                            Html.div [
                                prop.className "modal-box"
                                prop.children [
                                    Html.h3 [
                                        prop.className "font-bold text-lg mb-4"
                                        prop.text "Confirm Delete"
                                    ]
                                    Html.p [
                                        prop.className "py-4"
                                        prop.text (sprintf "Are you sure you want to delete \"%s\"?" file.Name)
                                    ]
                                    Html.div [
                                        prop.className "modal-action"
                                        prop.children [
                                            Daisy.button.button [
                                                button.ghost
                                                prop.onClick (fun _ -> dispatch CancelDelete)
                                                prop.text "Cancel"
                                            ]
                                            Daisy.button.button [
                                                button.error
                                                prop.onClick (fun _ -> dispatch ConfirmDelete)
                                                prop.text "Delete"
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                | None -> Html.none


            // Upload Progress Modal
            UploadProgressModal uploadStatuses (fun () -> dispatch ClearUploadProgress)
        ]
    ]

