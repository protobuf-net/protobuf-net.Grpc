open ProtoBuf.Grpc.Client
open Shared_FS
open System
open System.Net.Http
open FSharp.Control.Tasks

[<EntryPoint>]
let main argv =
    HttpClientExtensions.AllowUnencryptedHttp2 <- true
    task {
        use http = new HttpClient (BaseAddress = Uri "http://localhost:10042")
        let client = http.CreateGrpcService<ICalculator>()
        let! result = client.MultiplyAsync { X = 12; Y = 4 }
        printfn "%i" result.Result
        return 0
    } |> fun t -> t.Result


// let main _ =
//     ClientFactory.AllowUnencryptedHttp2 <- true
//     use http = new HttpClient (BaseAddress = Uri "http://localhost:10042")
//     let client = ClientFactory.Create<ICalculator>(http)
//     let result = client.MultiplyAsync { X = 12; Y = 4 }
//     printfn "%i" result.Result.Result
//     0