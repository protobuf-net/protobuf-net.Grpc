namespace Server_FS

open Shared_FS
open System.Threading.Tasks

type MyCalculator() =
    interface ICalculator with 
        member this.MultiplyAsync(request : MultiplyRequest) =
            let result = new MultiplyResult( Result = request.X * request.Y )
            new ValueTask<MultiplyResult>(result)