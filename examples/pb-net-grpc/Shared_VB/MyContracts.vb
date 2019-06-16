Imports System.Runtime.Serialization
Imports System.ServiceModel

<ServiceContract(Name:="Hyper.Calculator")>
Public Interface ICalculator
    Function MultiplyAsync(request As MultiplyRequest) As ValueTask(Of MultiplyResult)
End Interface

<DataContract>
Public Class MultiplyRequest
    <DataMember(Order:=1)>
    Public Property X As Integer
    <DataMember(Order:=2)>
    Public Property Y As Integer
End Class

<DataContract>
Public Class MultiplyResult
    <DataMember(Order:=1)>
    Public Property Result As Integer
End Class
