# Register Client Service

## What is it?

> Client service lets you to inject Grpc client dependency to your controllers.

## Register client service

``` c#
public void ConfigureServices(IServiceCollection services)
{
    // other service setup above ...

    //this registers ICalculator Grpc client service
    services.AddCodeFirstGrpcClient<ICalculator>(o => {
        
        // Address of grpc server
        o.Address = new Uri(Configuration.GetValue<string>("https://localhost:5001"));
        
        // another channel options (based on best practices docs on https://docs.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0)
        o.ChannelOptionsActions.Add(options =>
        {
            options.HttpHandler = new SocketsHttpHandler()
            {
                // keeps connection alive
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                
                // allows channel to add additional HTTP/2 connections
                EnableMultipleHttp2Connections = true
            };
        });
    });
}
```

## How use it?

Then in your controller you can use the registered dependency.

``` c#
public class GrpcController : Controller
{
    private readonly ICalculator _calculatorClient;
    public GrpcController(ICalculator calculatorClient)
    {
        _calculatorClient = calculatorClient;
    }
    
    //otestovat
    public async Task<int> Multiply(int x, int y)
    {
        
        var result = await _calculatorClient.MultiplyAsync(new MultiplyRequest { X = x, Y = y });

        return result;
    }
}
```
