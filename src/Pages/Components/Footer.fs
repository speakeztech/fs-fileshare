module FS.FileShare.Pages.Components.Footer

open Feliz

[<ReactComponent>]
let Footer () =
    Html.footer [
        prop.className "w-full py-4"
        prop.children [
            Html.div [
                prop.className "w-full max-w-screen-2xl mx-auto px-4 flex justify-between items-center text-xs opacity-60"
                prop.children [
                    Html.div [
                        prop.children [
                            Html.text "Made with "
                            Html.a [
                                prop.href "https://github.com/speakeztech/CloudflareFS"
                                prop.target "_blank"
                                prop.rel "noopener noreferrer"
                                prop.className "link link-hover"
                                prop.text "CloudflareFS"
                            ]
                        ]
                    ]
                    Html.div [
                        prop.text "Copyright © 2025 SpeakEZ Technologies, Inc. All Rights Reserved."
                    ]
                ]
            ]
        ]
    ]
