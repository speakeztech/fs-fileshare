module FS.FileShare.Pages.Components.Breadcrumb

open Feliz
open Feliz.DaisyUI

[<ReactComponent>]
let Breadcrumb (currentPath: string option) (onNavigate: string option -> unit) =
    let pathParts =
        match currentPath with
        | None -> []
        | Some path ->
            path.Split('/')
            |> Array.filter (fun s -> s <> "")
            |> Array.toList

    let buildPath (index: int) =
        if index = -1 then
            None
        else
            pathParts.[0..index]
            |> String.concat "/"
            |> Some

    Daisy.breadcrumbs [
        Html.ul [
            // Root
            Html.li [
                Html.a [
                    prop.className "link link-hover"
                    prop.onClick (fun _ -> onNavigate None)
                    prop.text "Home"
                ]
            ]

            // Path segments
            for i, part in List.indexed pathParts do
                Html.li [
                    if i = pathParts.Length - 1 then
                        // Current directory (no link)
                        Html.span [
                            prop.className "font-bold"
                            prop.text part
                        ]
                    else
                        // Parent directories (clickable)
                        Html.a [
                            prop.className "link link-hover"
                            prop.onClick (fun _ -> onNavigate (buildPath i))
                            prop.text part
                        ]
                ]
        ]
    ]
