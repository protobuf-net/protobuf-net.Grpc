namespace Server_FS

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open ProtoBuf.Grpc.Server

type Startup() =

    /// This method gets called by the runtime. Use this method to add services to the container.
    /// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member __.ConfigureServices(services: IServiceCollection) =
        services.AddGrpc() |> ignore
        services.AddCodeFirstGrpc()

    /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member __.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if env.IsDevelopment() then app.UseDeveloperExceptionPage() |> ignore

        app.UseRouting()
           .UseEndpoints(fun endpoints -> endpoints.MapGrpcService<MyCalculator>() |> ignore)
           |> ignore