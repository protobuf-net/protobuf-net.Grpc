Imports System.Net.Http
Imports ProtoBuf.Grpc.Client
Imports Shared_VB

Module Program
    Sub Main()
        DoIt().Wait()
    End Sub

    Async Function DoIt() As Task
        HttpClientFactory.AllowUnencryptedHttp2 = True
        Using http As New HttpClient With {.BaseAddress = New Uri("http://localhost:10042")}
            Dim client = HttpClientFactory.Create(Of ICalculator)(http)
            Dim result = Await client.MultiplyAsync(New MultiplyRequest With {.X = 12, .Y = 4})
            Console.WriteLine(result.Result)
        End Using
    End Function
End Module
