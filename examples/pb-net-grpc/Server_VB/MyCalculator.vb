Imports Shared_VB
Public Class MyCalculator
    Implements ICalculator

    Function MultiplyAsync(request As MultiplyRequest) As ValueTask(Of MultiplyResult) Implements ICalculator.MultiplyAsync
        Dim result = New MultiplyResult With {.result = request.X * request.Y}
        Return New ValueTask(Of MultiplyResult)(result)
    End Function

End Class
