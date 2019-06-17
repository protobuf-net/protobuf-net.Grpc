namespace Shared_FS

open System.ServiceModel
open System.Threading.Tasks
open System.Runtime.Serialization

[<DataContract>]
type MultiplyRequest =
    {   [<DataMember(Order = 1)>] mutable X : int;
        [<DataMember(Order = 2)>] mutable Y : int;
    }
    
[<DataContract>]
type MultiplyResult =
    {   [<DataMember(Order = 1)>] mutable Result : int;
    }

[<ServiceContract(Name = "Hyper.Calculator")>]
type ICalculator =
    abstract MultiplyAsync : request : MultiplyRequest -> ValueTask<MultiplyResult>
    abstract Multiply : request : MultiplyRequest -> MultiplyResult

