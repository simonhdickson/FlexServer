module ServerCode.Server

open System.IO
open Suave
open Suave.Logging
open System.Net
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors
open Suave.Writers

let startServer clientPath =
    if not (Directory.Exists clientPath) then
        failwithf "Client-HomePath '%s' doesn't exist." clientPath

    let outPath = Path.Combine(clientPath,"public")
    if not (Directory.Exists outPath) then
        failwithf "Out-HomePath '%s' doesn't exist." outPath

    if Directory.EnumerateFiles outPath |> Seq.isEmpty then
        failwithf "Out-HomePath '%s' is empty." outPath

    let logger = Logging.Targets.create Logging.Info [| "Suave" |]

    let mimeTypes =
        defaultMimeTypesMap
            @@ (function | ".mp4" -> createMimeType "video/mp4" false  | _ -> None)
            @@ (function | ".mkv" -> createMimeType "video/webm" false | _ -> None)
            @@ (function | ".m4v" -> createMimeType "video/mp4" false  | _ -> None)

    let serverConfig =
        { defaultConfig with
            logger = Targets.create LogLevel.Debug [||]
            homeFolder = Some clientPath
            bindings = [ HttpBinding.create HTTP (IPAddress.Parse "0.0.0.0") 8085us]
            mimeTypesMap = mimeTypes }

    let app =
        choose [
            Movies.webPart
        ] >=> logWithLevelStructured Logging.Info logger logFormatStructured

    startWebServer serverConfig app