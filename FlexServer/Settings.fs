[<AutoOpen>]
module Settings
open FSharp.Data

module Internal =
    type SettingsJson = JsonProvider<"settings.json">

let Settings = Internal.SettingsJson.Load("settings.json")
