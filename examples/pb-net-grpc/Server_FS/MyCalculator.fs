namespace Server_FS

open Shared_FS
open System.Threading.Tasks

type MyCalculator() =
    interface ICalculator with 
        member __.MultiplyAsync(request : MultiplyRequest) =
            ValueTask<_>(MultiplyResult(Result = request.X * request.Y))