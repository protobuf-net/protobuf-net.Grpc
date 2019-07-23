Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.Extensions.DependencyInjection
Imports ProtoBuf.Grpc.Server

Public Class Startup
    Public Sub ConfigureServices(services As IServiceCollection)
        services.AddCodeFirstGrpc()
    End Sub

#Disable Warning IDE0060 ' Remove unused parameter
    Public Sub Configure(app As IApplicationBuilder, env As IWebHostEnvironment)
#Enable Warning IDE0060 ' Remove unused parameter
        app.UseRouting()

        app.UseEndpoints(
            Sub(endpoints)
                endpoints.MapGrpcService(Of MyCalculator)()
            End Sub)
        End Sub
End Class
