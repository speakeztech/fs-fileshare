module FS.FileShare.Pages.Components.UploadZone

open Fable.Core
open Fable.Core.JsInterop
open Feliz
open Feliz.DaisyUI
open Browser.Types

[<ReactComponent>]
let UploadZone (onFilesSelected: File array -> unit) =
    let (isDragging, setIsDragging) = React.useState false
    let fileInputRef = React.useRef<HTMLInputElement option>(None)

    let handleDragOver (e: DragEvent) =
        e.preventDefault()
        e.stopPropagation()
        setIsDragging true

    let handleDragLeave (e: DragEvent) =
        e.preventDefault()
        e.stopPropagation()
        setIsDragging false

    let handleDrop (e: DragEvent) =
        e.preventDefault()
        e.stopPropagation()
        setIsDragging false

        let dt: obj = e?dataTransfer
        if not (isNull dt) then
            let files: obj = dt?files
            let length: int = files?length
            let fileArray = [| for i in 0 .. length - 1 -> unbox<File> (files?item(i)) |]
            onFilesSelected fileArray

    let handleFileInput (e: Event) =
        let input: obj = e?target
        let files: obj = input?files
        if not (isNull files) then
            let length: int = files?length
            let fileArray = [| for i in 0 .. length - 1 -> unbox<File> (files?item(i)) |]
            onFilesSelected fileArray

    let handleClick () =
        match fileInputRef.current with
        | Some input -> input.click()
        | None -> ()

    Html.div [
        prop.className (
            if isDragging then
                "border-4 border-dashed border-primary bg-base-200 rounded-lg p-8 text-center cursor-pointer transition-all"
            else
                "border-2 border-dashed border-base-300 rounded-lg p-8 text-center cursor-pointer hover:border-primary hover:bg-base-200 transition-all"
        )
        prop.onDragOver handleDragOver
        prop.onDragLeave handleDragLeave
        prop.onDrop handleDrop
        prop.onClick (fun _ -> handleClick())
        prop.children [
            Html.input [
                prop.ref fileInputRef
                prop.type' "file"
                prop.multiple true
                prop.className "hidden"
                prop.onChange handleFileInput
            ]

            Html.div [
                prop.className "flex flex-col items-center gap-4"
                prop.children [
                    Html.i [
                        prop.className "fa-solid fa-cloud-arrow-up fa-3x text-primary"
                    ]

                    Html.div [
                        prop.className "text-lg font-medium"
                        prop.text (if isDragging then "Drop files here" else "Drag & drop files here")
                    ]

                    Html.div [
                        prop.className "text-sm text-base-content/70"
                        prop.text "or click to browse"
                    ]
                ]
            ]
        ]
    ]
