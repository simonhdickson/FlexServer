module Movies

open System.IO
open Suave
open Suave.Filters
open Suave.Json
open Suave.Operators
open Suave.Successful
open Suave.Utils
open Suave.Writers
open Newtonsoft.Json
open Suave.Sockets.Control
open SuaveExtensions

let folder = """D:\Content\Video\Movies"""

let write status dto : WebPart =
    status (JsonConvert.SerializeObject dto) >=> setMimeType "application/json; charset=utf-8"

type Movie = {
    name:string
    url:string
}

let movies =
    Directory.EnumerateFiles folder
    |> Seq.map (fun file ->
        { name = Path.GetFileNameWithoutExtension file
          url = sprintf "http://127.0.0.1:8080/movies/%s" <| Path.GetFileName file })

let page = sprintf """<head>
  <link href="http://vjs.zencdn.net/5.17.0/video-js.css" rel="stylesheet">

  <!-- If you'd like to support IE8 -->
  <script src="http://vjs.zencdn.net/ie8/1.1.2/videojs-ie8.min.js"></script>
</head>

<body>
  <video id="my-video" class="video-js" controls preload="auto" poster="MY_VIDEO_POSTER.jpg" data-setup="{}">
    <source src="%s" type='video/mp4'>
    <p class="vjs-no-js">
      To view this video please enable JavaScript, and consider upgrading to a web browser that
      <a href="http://videojs.com/html5-video-support/" target="_blank">supports HTML5 video</a>
    </p>
  </video>

  <script src="http://vjs.zencdn.net/5.17.0/video.js"></script>
</body>"""

let webPart : WebPart<HttpContext> =
    choose [
        GET >=> path "/movies" >=> write OK movies
        GET >=> pathScan "/movies/%s" (fun f -> partialFile <| Path.Combine(folder, f))
        GET >=> pathScan "/movies/%s/player" (fun f -> OK (page (sprintf "http://127.0.0.1:8080/movies/%s" f))) >=> setMimeType "text/html; charset=utf-8" 
    ]
