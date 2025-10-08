module FS.FileShare.Pages.App

open Feliz
open Fable.Core.JsInterop

// Import Tailwind CSS
importSideEffects "./styles.css"

ReactDOM.createRoot(Browser.Dom.document.getElementById("app")).render(View.AppView())
