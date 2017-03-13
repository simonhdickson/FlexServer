[<AutoOpen>]
module Settings
open System.IO

open Newtonsoft.Json

module Internal =
    type SettingsFolders = {
        Movies: string[]
    }
    type Settings = {
        Folders: SettingsFolders
    }

let Settings =
    JsonConvert.DeserializeObject<Internal.Settings>(
        File.ReadAllText("settings.json"))
