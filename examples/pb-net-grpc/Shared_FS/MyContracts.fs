namespace Shared_FS

open System.ServiceModel
open System.Threading.Tasks
open System.Runtime.Serialization

[<DataContract>]
type MultiplyRequest() =
    let mutable _x : int = 0
    let mutable _y : int = 0
    
    [<DataMember(Order = 1)>]
    member public l.X
        with get() = _x
        and set(value) = _x <- value

    [<DataMember(Order = 2)>]
    member public l.Y
        with get() = _y
        and set(value) = _y <- value
    
[<DataContract>]
type MultiplyResult() =
    let mutable _result : int = 0

    [<DataMember(Order = 1)>]
    member public l.Result
        with get() = _result
        and set(value) = _result <- value

[<ServiceContract(Name = "Hyper.Calculator")>]
type ICalculator =
    abstract MultiplyAsync : request : MultiplyRequest -> ValueTask<MultiplyResult>
    abstract Multiply : request : MultiplyRequest -> MultiplyResult

