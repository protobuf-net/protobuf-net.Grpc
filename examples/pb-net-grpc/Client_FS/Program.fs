open ProtoBuf.Grpc.Client
open Shared_FS
open Grpc.Net.Client
open FSharp.Control.Tasks

[<EntryPoint>]
let main _ =
    GrpcClientFactory.AllowUnencryptedHttp2 <- true
    task {
        use http = GrpcChannel.ForAddress("http://localhost:10042")
        let client = http.CreateGrpcService<ICalculator>()
        let! result = client.MultiplyAsync { X = 12; Y = 4 }
        printfn "%i" result.Result
        return 0
    } |> fun t -> t.Result