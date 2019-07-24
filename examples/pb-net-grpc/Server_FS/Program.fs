open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core
open Server_FS

let createHostBuilder args =
    WebHost.CreateDefaultBuilder(args)
           .ConfigureKestrel(fun options ->
                options.ListenLocalhost(10042, fun listenOptions ->
                    listenOptions.Protocols <- HttpProtocols.Http2
                ))
           .UseStartup<Startup>()

[<EntryPoint>]
let main args =
    createHostBuilder(args).Build().Run()
    0