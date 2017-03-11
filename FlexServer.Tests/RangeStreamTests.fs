module FlexServer.RangeStreamTests

open Xunit
open FsCheck.Xunit
open System
open System.IO
open FsCheck
open SuaveExtensions
open System.Collections.Generic

[<Property>]
let test (content:byte[]) (PositiveInt length) =
    let nc = Gen.elements [0..max 0 (content.Length-1)] |> Arb.fromGen

    Prop.forAll nc (fun start ->
        let length = min (content.Length - start) length

        let expected = Array.zeroCreate length
        Array.Copy(content, start, expected, 0, length)

        use rangedStream = new RangedStream(new MemoryStream(content), int64 start, Some (int64 length), true)
        let bytes = Array.zeroCreate (int rangedStream.Length)
        rangedStream.Read(bytes, 0, int rangedStream.Length) |> ignore

        Assert.Equal<IEnumerable<byte>>(expected, bytes))
