module FS.FileShare.Pages.Components.UploadProgressModal

open Feliz
open Feliz.DaisyUI

type UploadStatus = {
    Filename: string
    Progress: float
    IsComplete: bool
    HasError: bool
}

[<ReactComponent>]
let UploadProgressModal (uploads: UploadStatus list) (onClose: unit -> unit) =
    let isOpen = uploads.Length > 0
    let allComplete = uploads |> List.forall (fun u -> u.IsComplete)
    let hasErrors = uploads |> List.exists (fun u -> u.HasError)

    let totalProgress =
        if uploads.Length = 0 then
            0.0
        else
            (uploads |> List.sumBy (fun u -> u.Progress)) / float uploads.Length

    if isOpen then
        Html.div [
            prop.className "modal modal-open"
            prop.children [
                Html.div [
                    prop.className "modal-box max-w-2xl"
                    prop.children [
                        Html.h3 [
                            prop.className "font-bold text-lg mb-4"
                            prop.text (if allComplete then "Upload Complete" else "Uploading Files...")
                        ]

                        // Overall progress
                        Html.div [
                            prop.className "mb-6"
                            prop.children [
                                Html.div [
                                    prop.className "flex justify-between text-sm mb-2"
                                    prop.children [
                                        Html.span $"Overall Progress: {uploads |> List.filter (fun u -> u.IsComplete) |> List.length}/{uploads.Length} files"
                                    ]
                                ]
                            ]
                        ]

                        // Individual file progress
                        Html.div [
                            prop.className "max-h-96 overflow-y-auto space-y-3"
                            prop.children (
                                uploads |> List.map (fun upload ->
                                    Html.div [
                                        prop.key upload.Filename
                                        prop.className "bg-base-200 p-3 rounded-lg"
                                        prop.children [
                                            Html.div [
                                                prop.className "flex items-center gap-2 mb-2"
                                                prop.children [
                                                    if upload.HasError || upload.IsComplete then
                                                        Html.i [
                                                            prop.className (
                                                                if upload.HasError then "fa-solid fa-circle-xmark text-error"
                                                                else "fa-solid fa-circle-check text-success"
                                                            )
                                                        ]
                                                    Html.span [
                                                        prop.className "text-sm font-medium flex-1 truncate"
                                                        prop.text upload.Filename
                                                    ]
                                                    Html.span [
                                                        prop.className "text-xs"
                                                        prop.text (
                                                            if upload.HasError then "Failed"
                                                            elif upload.IsComplete then "Complete"
                                                            else "Uploading..."
                                                        )
                                                    ]
                                                ]
                                            ]
                                            if not upload.IsComplete && not upload.HasError then
                                                Html.progress [
                                                    prop.className "progress progress-primary w-full h-2"
                                                ]
                                        ]
                                    ]
                                )
                            )
                        ]

                        // Close button (only show when all complete or has errors)
                        if allComplete || hasErrors then
                            Html.div [
                                prop.className "modal-action"
                                prop.children [
                                    Daisy.button.button [
                                        button.primary
                                        prop.onClick (fun _ -> onClose())
                                        prop.text "Close"
                                    ]
                                ]
                            ]
                    ]
                ]
            ]
        ]
    else
        Html.none
