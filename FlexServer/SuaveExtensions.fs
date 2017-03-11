module SuaveExtensions

open System
open System.Text
open System.IO
open Suave
open Suave.Sockets.Control
open Suave.Sockets
open Suave.Embedded

type RangedStream(stream:Stream, start, limit, ?disposeInner) =
    inherit Stream()
    do stream.Position <- start
    let endPosition = min stream.Length (stream.Position + limit)
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
    override __.Close() =
        disposeInner
        |> Option.iter (fun i -> if i then stream.Close())

let sendFile (start:int, finish) fileName (compression : bool) (ctx : HttpContext) =
    let writeFile file (conn, _) = socket {
        let length = finish - start
        let getFs = fun path ->
            use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            fs.Seek(int64 start, SeekOrigin.Begin) |> ignore
            let buffer = Array.zeroCreate length
            fs.Read(buffer, 0, length) |> ignore
            fs.Flush()
            new MemoryStream(buffer) :> Stream
        let getLm = fun path -> FileInfo(path).LastWriteTime
        let! (encoding, fs) = Compression.transformStream file getFs getLm compression ctx.runtime.compressionFolder ctx
        try
            match encoding with
            | Some n ->
                let! (_,conn) = asyncWriteLn (String.Concat [| "Content-Encoding: "; n.ToString() |]) conn
                let! (_,conn) = asyncWriteLn (sprintf "Content-Length: %d\r\n" (fs : Stream).Length) conn
                let! conn = flush conn
                if  ctx.request.``method`` <> HttpMethod.HEAD && fs.Length > 0L then
                    do! transferStream conn fs
                return conn
            | None ->
                let! (_,conn) = asyncWriteLn (sprintf "Content-Length: %d\r\n" (fs : Stream).Length) conn
                let! conn = flush conn
                if  ctx.request.``method`` <> HttpMethod.HEAD && fs.Length > 0L then
                    do! transferStream conn fs
                return conn
      finally
        fs.Dispose()
    }
    { ctx with
        response =
          { ctx.response with
              status = HTTP_200.status
              content = SocketTask (writeFile fileName) } }
    |> succeed

let (|ContentRange|_|)  (context:HttpContext) =
    match context.request.header "content-range" with
    | Choice1Of2 range ->
        let result = range.Split(' ', '/', '-')
        if result.Length = 4 then
            Some (int result.[1], int result.[2])
        else
            None
    | Choice2Of2 _ -> None

let partialFile file (context:HttpContext) =
    match context with
    | ContentRange range -> sendFile range file true context
    | _ -> Files.file file context
