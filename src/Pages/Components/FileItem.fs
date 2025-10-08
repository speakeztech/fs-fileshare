module FS.FileShare.Pages.Components.FileItem

open Feliz
open Feliz.DaisyUI
open FS.FileShare.Pages.FileApi
open FS.FileShare.Pages.Components.FileIcon

type FileItemMsg =
    | Download
    | Delete
    | Navigate

[<ReactComponent>]
let FileItem (file: FileInfo) (onAction: FileItemMsg -> unit) =
    let formatSize (bytes: int64) =
        if file.IsDirectory then
            "-"
        else
            let kb = float bytes / 1024.0
            if kb < 1024.0 then
                $"%.2f{kb} KB"
            else
                let mb = kb / 1024.0
                if mb < 1024.0 then
                    $"%.2f{mb} MB"
                else
                    let gb = mb / 1024.0
                    $"%.2f{gb} GB"

    Html.tr [
        prop.className "hover"
        prop.children [
            // Icon + Name
            Html.td [
                Html.div [
                    prop.className (if file.IsDirectory then "flex items-center gap-3 cursor-pointer" else "flex items-center gap-3")
                    prop.onClick (fun _ ->
                        if file.IsDirectory then
                            onAction Navigate
                    )
                    prop.children [
                        FileIcon file.Name file.IsDirectory
                        Html.span [
                            prop.className "font-medium"
                            prop.text file.Name
                        ]
                    ]
                ]
            ]

            // Size
            Html.td [
                prop.text (formatSize file.Size)
            ]

            // Last Modified
            Html.td [
                prop.text file.LastModified
            ]

            // Actions
            Html.td [
                Html.div [
                    prop.className "flex gap-2"
                    prop.children [
                        if file.IsDirectory then
                            // Invisible placeholder to align delete button with files
                            Daisy.button.button [
                                button.ghost
                                button.sm
                                prop.className "invisible"
                                prop.disabled true
                                prop.children [
                                    Html.i [ prop.className "fa-solid fa-download" ]
                                ]
                            ]
                        else
                            Daisy.button.button [
                                button.ghost
                                button.sm
                                prop.onClick (fun _ -> onAction Download)
                                prop.children [
                                    Html.i [ prop.className "fa-solid fa-download" ]
                                ]
                            ]

                        Daisy.button.button [
                            button.ghost
                            button.sm
                            prop.onClick (fun _ -> onAction Delete)
                            prop.className "text-error"
                            prop.children [
                                Html.i [ prop.className "fa-solid fa-trash" ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
