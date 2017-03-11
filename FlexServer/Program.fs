open Suave
open Suave.Operators
open Suave.Writers

let webPart =
    choose [
        Movies.webPart
    ]

let mimeTypes =
    defaultMimeTypesMap
        @@ (function | ".mp4" -> createMimeType  "video/mp4" false | _ -> None)
        @@ (function | ".mkv" -> createMimeType  "video/webm" false | _ -> None)
        @@ (function | ".m4v" -> createMimeType  "video/mp4" false | _ -> None)

let webConfig = 
    {
        defaultConfig with
            mimeTypesMap = mimeTypes
    }

startWebServer webConfig webPart
