module FS.FileShare.Pages.Pages.Index

open Feliz
open FS.FileShare.Pages.Components.FileManager

[<ReactComponent>]
let IndexView (username: string) (password: string) =
    Html.div [
        prop.className "h-full p-4"
        prop.children [
            FileManager username password
        ]
    ]
