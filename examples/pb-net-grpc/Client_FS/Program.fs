open System
open ProtoBuf.Grpc.Client
open Shared_FS
open System.Net.Http


[<EntryPoint>]
let main argv : int =
    ClientFactory.AllowUnencryptedHttp2 <- true
    use http = new HttpClient ( BaseAddress = new Uri("http://localhost:10042") )
    let client = ClientFactory.Create<ICalculator>(http)
    let result = client.Multiply(new MultiplyRequest(X = 12, Y = 4)) 
    printfn "%i" result.Result
    0 // return an integer exit code