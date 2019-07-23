namespace Shared_FS

open System.ServiceModel
open System.Threading.Tasks
open System.Runtime.Serialization

[<DataContract; CLIMutable>]
type MultiplyRequest =
    { [<DataMember(Order = 1)>] X : int
      [<DataMember(Order = 2)>] Y : int }
    
[<DataContract; CLIMutable>]
type MultiplyResult =
    { [<DataMember(Order = 1)>] Result : int }

[<ServiceContract(Name = "Hyper.Calculator")>]
type ICalculator =
    abstract MultiplyAsync : MultiplyRequest -> ValueTask<MultiplyResult>

