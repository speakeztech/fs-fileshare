module FS.FileShare.Pages.Router

open Browser.Types
open Feliz.Router
open Fable.Core.JsInterop

type Page =
    | FileManager of path: string option

[<RequireQualifiedAccess>]
module Page =
    let defaultPage = Page.FileManager None

    let parseFromUrlSegments = function
        | [] -> Page.FileManager None
        | "browse" :: rest ->
            let path = String.concat "/" rest
            Page.FileManager (if path = "" then None else Some path)
        | _ -> defaultPage

    let noQueryString segments : string list * (string * string) list = segments, []

    let toUrlSegments = function
        | Page.FileManager None -> [] |> noQueryString
        | Page.FileManager (Some path) ->
            let parts = path.Split('/') |> Array.toList
            "browse" :: parts |> noQueryString

[<RequireQualifiedAccess>]
module Router =
    let goToUrl (e: MouseEvent) =
        e.preventDefault()
        let href: string = !!e.currentTarget?attributes?href?value
        Router.navigatePath href

    let navigatePage (p: Page) = p |> Page.toUrlSegments |> Router.navigatePath

[<RequireQualifiedAccess>]
module Cmd =
    let navigatePage (p: Page) = p |> Page.toUrlSegments |> Cmd.navigatePath
