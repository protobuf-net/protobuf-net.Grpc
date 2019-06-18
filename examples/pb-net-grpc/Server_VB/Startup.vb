Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.Extensions.DependencyInjection
Imports ProtoBuf.Grpc.Server

Public Class Startup
    Public Sub ConfigureServices(services As IServiceCollection)
        services.AddGrpc()
        services.AddCodeFirstGrpc()
    End Sub

    Public Sub Configure(app As IApplicationBuilder, env As IWebHostEnvironment)
        app.UseRouting()

        app.UseEndpoints(
            Sub(endpoints)
                endpoints.MapGrpcService(Of MyCalculator)()
            End Sub)
        End Sub
End Class
