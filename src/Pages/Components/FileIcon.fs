module FS.FileShare.Pages.Components.FileIcon

open Feliz
open Feliz.DaisyUI

/// Get icon class based on file type
let getIconClass (filename: string) (isDirectory: bool) : string =
    if isDirectory then
        "fa-solid fa-folder text-warning"
    else
        // Handle null/empty filename
        if System.String.IsNullOrEmpty(filename) then
            "fa-solid fa-file"
        else
            let ext =
                if filename.Contains(".") then
                    filename.Split('.')
                    |> Array.last
                    |> fun s -> s.ToLower()
                else
                    ""

            match ext with
            | "pdf" -> "fa-solid fa-file-pdf text-error"
            | "doc" | "docx" -> "fa-solid fa-file-word text-info"
            | "xls" | "xlsx" -> "fa-solid fa-file-excel text-success"
            | "ppt" | "pptx" -> "fa-solid fa-file-powerpoint text-warning"
            | "zip" | "rar" | "7z" | "tar" | "gz" -> "fa-solid fa-file-zipper text-secondary"
            | "jpg" | "jpeg" | "png" | "gif" | "bmp" | "svg" -> "fa-solid fa-file-image text-primary"
            | "mp4" | "avi" | "mov" | "wmv" -> "fa-solid fa-file-video text-accent"
            | "mp3" | "wav" | "flac" | "ogg" -> "fa-solid fa-file-audio text-accent"
            | "txt" | "md" -> "fa-solid fa-file-lines"
            | "html" | "css" | "js" | "ts" | "jsx" | "tsx" | "json" | "xml" -> "fa-solid fa-file-code text-info"
            | "cs" | "fs" | "py" | "java" | "cpp" | "c" | "h" -> "fa-solid fa-file-code text-success"
            | _ -> "fa-solid fa-file"

[<ReactComponent>]
let FileIcon (filename: string) (isDirectory: bool) =
    Html.i [
        prop.className (getIconClass filename isDirectory)
    ]
