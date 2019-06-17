namespace Server_FS

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core

module Program =
    let exitCode = 0

    let CreateHostBuilder args =
        WebHost.CreateDefaultBuilder(args)
            .ConfigureKestrel(fun options ->
                options.ListenLocalhost(10042, fun listenOptions->
                    listenOptions.Protocols <- HttpProtocols.Http2;
                )
            ).UseStartup<Startup>()

    [<EntryPoint>]
    let main args =
        CreateHostBuilder(args).Build().Run()

        exitCode
