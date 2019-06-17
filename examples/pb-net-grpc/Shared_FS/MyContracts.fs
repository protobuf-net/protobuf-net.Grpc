namespace Shared_FS

open System.ServiceModel
open System.Threading.Tasks
open System.Runtime.Serialization

[<DataContract>]
type MultiplyRequest() =    
    [<DataMember(Order = 1)>]
    member val public X = 0 with get, set
    [<DataMember(Order = 2)>]
    member val public Y = 0 with get, set
    
[<DataContract>]
type MultiplyResult() =
    [<DataMember(Order = 1)>]
    member val public Result = 0 with get, set

[<ServiceContract(Name = "Hyper.Calculator")>]
type ICalculator =
    abstract MultiplyAsync : request : MultiplyRequest -> ValueTask<MultiplyResult>

