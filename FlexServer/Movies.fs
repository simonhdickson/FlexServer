module Movies

open System.IO
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Utils
open Suave.Writers
open Newtonsoft.Json
open SuaveExtensions
open FSharp.Data

let folder = Settings.Folders.Movies.[0]

let asHtml = setMimeType "text/html; charset=utf-8"
let asJson = setMimeType "application/json; charset=utf-8"

let write status dto : WebPart =
    status (JsonConvert.SerializeObject dto) >=> asJson

type Movie = {
    name:string
    url:string
}

let movies =
    Directory.EnumerateFiles folder
    |> Seq.map (fun file ->
        { name = Path.GetFileNameWithoutExtension file
          url = sprintf "http://127.0.0.1:8080/movies/%s" <| Path.GetFileName file })

let page = sprintf """<!DOCTYPE html>
<body>
  <video id="my-video" class="afterglow">
    <source src="%s" type='video/mp4'>
  </video>
  <script src="//cdn.jsdelivr.net/afterglow/latest/afterglow.min.js"></script>
</body>"""

let webPart : WebPart<HttpContext> =
    choose [
        GET >=> path "/movies" >=> write OK movies
        GET >=> pathScan "/movies/%s" (fun f -> partialFile <| Path.Combine(folder, f))
        GET >=> pathScan "/movies/%s/player" (fun f -> OK (page (sprintf "http://127.0.0.1:8080/movies/%s" f))) >=> asHtml
    ]
