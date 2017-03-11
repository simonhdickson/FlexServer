module SuaveExtensions

open System
open System.Net.Http.Headers
open System.IO
open System.Text
open Suave
open Suave.Sockets.Control
open Suave.Sockets
open Suave.Embedded

type RangedStream(stream:Stream, start, limit, ?disposeInner) =
    inherit Stream()
    do stream.Position <- start
    let endPosition =
        match limit with
        | Some limit -> min stream.Length (stream.Position + limit)
        | None -> stream.Length
    let maxRead count =
        let maxCount = endPosition - stream.Position
        if count > maxCount then maxCount else count
    override __.CanRead = stream.CanRead
    override __.CanSeek = stream.CanSeek
    override __.CanWrite = stream.CanWrite
    override __.Flush() = stream.Flush()
    override __.Length with get () = int64 (endPosition - start)
    override __.Position with get () = stream.Position and set value = stream.Position <- value
    override __.Seek(offset, origin) = stream.Seek(offset, origin)
    override __.SetLength(value) = failwith ""
    override __.Read(buffer, offset, count) = stream.Read(buffer, offset, int <| maxRead (int64 count))
    override __.Write(buffer, offset, count) = failwith ""
    override __.CanTimeout = stream.CanTimeout
    override __.ReadTimeout with get () = stream.ReadTimeout and set value = stream.ReadTimeout <- value
    override __.WriteTimeout with get () = stream.WriteTimeout and set value = stream.WriteTimeout <- value
    override __.BeginRead(buffer, offset, count, callback, state) =
        stream.BeginRead(buffer, offset, int <| maxRead (int64 count), callback, state)
    override __.EndRead(result) = stream.EndRead(result)
    override __.Close() =
        disposeInner
        |> Option.iter (fun streamOwner -> if streamOwner then stream.Close())

let parseContentRange (input:string) =
    let contentUnit = input.Split([|' '; '='|], 2)
    let rangeArray = contentUnit.[1].Split([|'-'|])
    let start = int64 rangeArray.[0]
    let finish = if Int64.TryParse (rangeArray.[1], ref 0L) then Some <| int64 rangeArray.[1] else None
    start, finish
    
let (|ContentRange|_|) (context:HttpContext) =
    match context.request.header "range" with
    | Choice1Of2 rangeValue -> Some <| parseContentRange rangeValue
    | Choice2Of2 _ -> None

let sendFile (start:int64, finish) fileName (compression : bool) (ctx : HttpContext) =
    let writeFile file (conn, _) = socket {
        let length = finish |> Option.bind (fun finish -> Some (finish - start))
        let getFs = fun path ->
            let fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            new RangedStream(fs, start, length, true) :> Stream
            //new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) :> Stream
        let getLm = fun path -> FileInfo(path).LastWriteTime
        let! encoding, fs = Compression.transformStream file getFs getLm compression ctx.runtime.compressionFolder ctx
        try
            match encoding with
            | Some n ->
                let! (_,conn) = asyncWriteLn (sprintf "Content-Range: bytes %d-%d/*" start (start+fs.Length)) conn
                let! (_,conn) = asyncWriteLn (String.Concat [| "Content-Encoding: "; n.ToString() |]) conn
                let! (_,conn) = asyncWriteLn (sprintf "Content-Length: %d\r\n" fs.Length) conn
                let! conn = flush conn
                if  ctx.request.``method`` <> HEAD && fs.Length > 0L then
                    do! transferStream conn fs
                return conn
            | None ->
                let! (_,conn) = asyncWriteLn (sprintf "Content-Range: bytes %d-%d/*" start (start+fs.Length)) conn
                let! (_,conn) = asyncWriteLn (sprintf "Content-Length: %d\r\n" fs.Length) conn
                let! conn = flush conn
                if  ctx.request.``method`` <> HEAD && fs.Length > 0L then
                    do! transferStream conn fs
                return conn
      finally
        fs.Dispose()
    }
    { ctx with
        response =
          { ctx.response with
              status = HTTP_206.status
              content = SocketTask (writeFile fileName) } }
    |> succeed

open Writers
open Redirection
open RequestErrors
open Suave.Utils
open Suave.Logging
open Suave.Logging.Message
open Suave.Operators

let resource key exists getLast getExtension
                (send : string -> bool -> WebPart)
                ctx =
    let log =
      event Verbose
      >> setSingleName "Suave.Http.ServeResource.resource"
      >> ctx.runtime.logger.logSimple

    let sendIt name compression =
      setHeader "Last-Modified" ((getLast key : DateTime).ToString("R"))
      >=> setHeader "Vary" "Accept-Encoding"
      >=> setMimeType name
      >=> send key compression

    if exists key then
      let mimes = ctx.runtime.mimeTypesMap (getExtension key)
      match mimes with
      | Some value ->
        match ctx.request.header "if-modified-since" with
        | Choice1Of2 v ->
          match Parse.dateTime v with
          | Choice1Of2 date ->
            if getLast key > date then sendIt value.name value.compression ctx
            else NOT_MODIFIED ctx
          | Choice2Of2 parse_error -> bad_request [||] ctx
        | Choice2Of2 _ ->
          sendIt value.name value.compression ctx
      | None ->
        let ext = getExtension key
        log (sprintf "failed to find matching mime for ext '%s'" ext)
        fail
    else
      log (sprintf "failed to find resource by key '%s'" key)
      fail

let file2 range fileName =
    resource
      fileName
      (File.Exists)
      (fun name -> FileInfo(name).LastAccessTime)
      (Path.GetExtension)
      (sendFile range)
    
let partialFile file (context:HttpContext) =
    match context with
    | ContentRange range -> file2 range file context
    | _ -> Files.file file context
