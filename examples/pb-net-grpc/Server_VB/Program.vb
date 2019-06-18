Imports Microsoft.AspNetCore
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Server.Kestrel.Core
Imports Microsoft.Extensions.Hosting

Module Program
    Sub Main(args As String())
        CreateHostBuilder(args).Build().Run()
    End Sub
    Function CreateHostBuilder(args As String()) As IWebHostBuilder
        Return WebHost.CreateDefaultBuilder(args).ConfigureKestrel(
            Sub(options)
                options.ListenLocalhost(10042,
                    Sub(listenOptions)
                        listenOptions.Protocols = HttpProtocols.Http2
                    End Sub)
            End Sub).UseStartup(Of Startup)()
    End Function
End Module
