# protobuf-net.Grpc

## What is it?

> Simple gRPC access in .NET Core 3 - think WCF, but over gRPC

- Google released gRPC, a cross-platform RPC stack over HTTP/2 using protobuf serialization
- included in the Google bits is [`Grpc.Core`](https://github.com/grpc/grpc), Google's gRPC bindings for .NET; it has kinks:
  - the "protoc" codegen tool only offers C# (for .NET) and is proto3 only
  - contract-first only
  - the actual HTTP/2 bits are in an unmanaged binary
- Micrsoft are working on [`Grpc.Net`](https://github.com/grpc/grpc-dotnet)
  - it uses the same core types - so new or existing code based on `Grpc.Core` can work fine
  - it can use the "Kestrel" HTTP/2 server bindings, and the new `HttpClient` HTTP/2 client bindings
  - but it still has the other limitations from `Grpc.Core`

## So what is `protobuf-net.Grpc`?

Unrelated to gRPC; for many years now, [`protobuf-net`](https://github.com/mgravell/protobuf-net) has offered idiomatic
protobuf serialization for .NET; `protobuf-net.Grpc` takes the best bits from `protobuf-net` and `Grpc.Net` and smashes
them together to give you:

- the "Kestrel" and `HttpClient` HTTP/2 bindings
- code-first or contract-first
- the "protogen" codegen tool is proto2 and proto3, and offers C# and VB
- and if you have code-first support, you can use any .NET language (tested: C#, VB, F#)

Additionally, it even works with the standard (unmanaged) `Grpc.Core` implementation if you are limited to .NET Framework (.NET Framework 4.6.1 or .NET Standard 2.0).

## I'm interested in code-first; how do I get started?

### 0: get your build environment

This work assumes you have the [.NET Core 3 preview](https://github.com/dotnet/core/blob/master/daily-builds.md), and an up-to-date compiler, ideally
by having the latest **preview** IDE (16.2.0 preview 2 at time of writing).

Also: make sure that you are *actually using* the preview runtime, via "global.json"; right now, my "global.json" is:

``` json
{
  "sdk": {
    "version": "3.0.100-preview7-012341"
  }
}
```

### 1: define your data contracts and service contracts

Your service and data contracts can be placed directly in the client/server (see later), or can be in a separate class library. If you use
a separate library, make sure you target `netcoreapp3.0`.

As for what they look like: think "WCF". Data contracts are classes marked with either `[ProtoContract]` or `[DataContract]`, with individual members
annotated with either `[ProtoMember]` or `[DataMember]`. The `[Proto*]` options are protobuf-net specific and offer fine-grained
control - and require a package-reference to [`protobuf-net`](https://www.nuget.org/packages/protobuf-net/); the `[Data*]` options are part
of the BCL and avoid needing an external reference (if you care), but may need [`System.ServiceModel.Primitives`](https://www.nuget.org/packages/System.ServiceModel.Primitives/) . **Key point**: protobuf
uses integer tokens to identify members (not names), so you need to tell the library how to map them (positive integers, unique per type:

``` c#
[DataContract]
public class MultiplyRequest
{
    [DataMember(Order = 1)]
    public int X { get; set; }

    [DataMember(Order = 2)]
    public int Y { get; set; }
}

[DataContract]
public class MultiplyResult
{
    [DataMember(Order = 1)]
    public int Result { get; set; }
}
```

Object models can be arbitrarily deep and complex (including lists, arrays, etc), but should be trees (not graphs).


Service contracts are interfaces marked with `[ServiceContract]`. You can optionally specify the gRPC service name, otherwise it'll use
reasonable convention-based defaults. Individual RPC calls are methods, which can optionally be marked with `[OperationContract]` to control
the name.

In gRPC, there are 4 types of call available:

- unary: the client sends one input; the server sends one output (classic request/response)
- client-streaming: the client can send a sequence of inputs; the server sends one output
- server-streaming: the client sends one input; the server sends a sequence of outputs
- duplex: the client and server can arbitrarily send disconnected sequences at each-other

Let's start with unary; a simple example there might be:

``` c#
[ServiceContract(Name = "Hyper.Calculator")]
public interface ICalculator
{
    ValueTask<MultiplyResult> MultiplyAsync(MultiplyRequest request);
}
```

We're using `ValueTask<T>` here, but the library also supports `Task<T>` and simple synchronous `T` (but: please prefer asynchronous when possible). Sometimes,
you don't actually have data to send in one or both directions; in regular gRPC you'd typically use `.google.protobuf.Empty` as a nil token here, but
we don't need to do that here - we can just have a parameterless method (`MultiplyAsync()`) and/or return `void`, `ValueTask` or `Task`. The library understands
what you intend. Additionally, since you may want to specify or query dealines, credentials, send/receive additional headers, trailers, etc, there is a `CallContext`
type that expresses this intent, which can be included as an additional parameter **after** the data parameter. Since you don't always need this, it is useful
to make this an optional parameter, i.e.

``` c#
[ServiceContract(Name = "Hyper.Calculator")]
public interface ICalculator
{
    ValueTask<MultiplyResult> MultiplyAsync(MultiplyRequest request, CallContext context = default);
}
```

If you want to use client/server-streaming or duplex communication, then instead of using `T`, `Task<T>` etc, you can use `IAsyncEnumerable<T>` for
either the data parameter or the return type. For example, we could subscribe to a speaking clock via:

``` c#
[ServiceContract]
public interface ITimeService
{
    IAsyncEnumerable<TimeResult> SubscribeAsync(CallContext context = default);
}

[ProtoContract]
public class TimeResult
{
    [ProtoMember(1, DataFormat = DataFormat.WellKnown)]
    public DateTime Time { get; set; }
}
```

The `IAsyncEnumerable<TimeResult>` is a server-streaming sequence of values; the `DataFormat.WellKnown` here tells `protobuf-net` to use the `.google.protobuf.Timestamp` representation
of time, which is recommended if you may need to work cross-platform (for legacy reasons, protobuf-net defaults to a different library-specific layout that pre-dates the
introduction of `.google.protobuf.Timestamp`). It is recommended to use `DataFormat.WellKnown` on `DateTime` and `TimeSpan` values whenever possible.

### 2: implement the server

1. Create an ASP.NET Core Web Application targeting `netcoreapp3.0`, and add a package references to [`protobuf-net.Grpc.AspNetCore`](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore)
(and a project/package reference to your data/service contracts if necessary). Note that the gRPC tooling can run alongside other services/sites that your ASP.NET application is providing.
2. in `CreateHostBuilder`, make sure you are using `WebHost`, and enable listening on `HttpProtocols.Http2`; see [`Program.cs`](https://github.com/mgravell/protobuf-net.Grpc/blob/master/examples/pb-net-grpc/Server_CS/Program.cs)
3. in `ConfigureServices`, call `services.AddGrpc()` and `services.AddCodeFirstGrpc()`; see [`Startup.cs`](https://github.com/mgravell/protobuf-net.Grpc/blob/master/examples/pb-net-grpc/Server_CS/Startup.cs)
4. define a class that implements your service contract, i.e. `class MyTimeService : ITimeService`
5. in `Configure`, call `endpoints.MapGrpcService<TService>()` for each of your services; see [`Startup.cs`](https://github.com/mgravell/protobuf-net.Grpc/blob/master/examples/pb-net-grpc/Server_CS/Startup.cs)

So what might our services look like? Let's start with our simple calculator; that might be synchronous at the server, which is why `ValueTask<T>` can be useful; consider:

``` c#
public class MyCalculator : ICalculator
{
    ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request)
        => new ValueTask<MultiplyResult>(new MultiplyResult { Result = request.X * request.Y });
}
```

Very simple. Note that a service type can implement any number of interfaces - any that are marked suitable as `[ServiceContract]` will be considered. Note also that the usual
ASP.NET dependency injection works, so if you need additional services you can request them via the constructor of the service type.

How about something more advanced, like our time service? This is a bit more subtle:

``` c#
public class MyTimeService : ITimeService
{
    public IAsyncEnumerable<TimeResult> SubscribeAsync(CallContext context = default)
        => SubscribeAsync(context.CancellationToken);

    private async IAsyncEnumerable<TimeResult> SubscribeAsync([EnumeratorCancellation] CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            yield return new TimeResult { Time = DateTime.UtcNow };
        }
    }
}
```

Here we're using an asynchronous iterator block (`yield return`) to generate a new result every ten seconds until cancellation. Note
that we're also using the `[EnumeratorCancellation]` syntax which helps the C# compiler create an iterator block that will behave
correctly. Cancellation is supported by the core gRPC framework, and is surfaced via `CancellationToken` in a familiar way.

Now all we need to do is use `dotnet run`, and we should be in business with our gRPC server:

```
info: ProtoBuf.Grpc.Server.ServicesExtensions.CodeFirstServiceMethodProvider[0]
      Server_CS.MyCalculator implementing service Hyper.Calculator (via 'ICalculator') with 1 operation(s)
dbug: Grpc.AspNetCore.Server.Model.Internal.ServiceRouteBuilder[1]
      Added gRPC method 'MultiplyAsync' to service 'Hyper.Calculator'. Method type: 'Unary', route pattern: 'Hyper.Calculator/Multiply'.
info: ProtoBuf.Grpc.Server.ServicesExtensions.CodeFirstServiceMethodProvider[0]
      Server_CS.MyTimeService implementing service MegaCorpTimeService (via 'ITimeService') with 1 operation(s)
dbug: Grpc.AspNetCore.Server.Model.Internal.ServiceRouteBuilder[1]
      Added gRPC method 'SubscribeAsync' to service 'MegaCorpTimeService'. Method type: 'ServerStreaming', route pattern: 'MegaCorpTimeService/Subscribe'.
...
Now listening on: http://localhost:10042
```

(note I'm using http, not https for this example; gRPC supports TLS and authentication - it just isn't something I'm covering)

### 2: implement the client

OK, we have a working server; now let's write a client. This is much easier, in fact. Let's create a .NET Core console application targeting `netcoreapp3.0`,
and add a package reference to [`protobuf-net.Grpc.HttpClient`](https://www.nuget.org/packages/protobuf-net.Grpc.HttpClient). Note that by default, `HttpClient` only wants to talk HTTP/2 over TLS, so we first
need to twist it's arm a little; then we can very easily create a client to our services at our base address; let's start by doing some maths:

``` c#
static async Task Main()
{
    HttpClientExtensions.AllowUnencryptedHttp2 = true;
    using (var http = new HttpClient { BaseAddress = new Uri("http://localhost:10042") })
    {
        var calculator = http.CreateGrpcService<ICalculator>();
        var result = await calculator.MultiplyAsync(new MultiplyRequest { X = 12, Y = 4 });
        Console.WriteLine(result.Result);
    }
}
```

If we use `dotnet run`, unsurprisingly we see:

```
48
```

So let's do something more exciting and consume our time service for, say, a minute:

``` c#
var clock = http.CreateGrpcService<ITimeService>();
var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(1));
var options = new CallOptions(cancellationToken: cancel.Token);
await foreach(var time in clock.SubscribeAsync(new CallContext(options)))
{
    Console.WriteLine($"The time is now: {time.Time}");
}
```

and now we see (with the lines appearing every 10 seconds, not all at once):

```
48
The time is now: 17/06/2019 18:44:43
The time is now: 17/06/2019 18:44:53
The time is now: 17/06/2019 18:45:03
The time is now: 17/06/2019 18:45:13
The time is now: 17/06/2019 18:45:23
```

As you would expect, `IAsyncEnumerable<T>` works similarly at the server, exposing values sent by the client as they
arrive. In both cases, the thread doesn't *block* while waiting for work - the `await` here ensures that this is
implemented using a continuation-based model.

### Did you mention that it works on .NET Framework too?

Yes; see [`protobuf-net.Grpc.Native`](https://www.nuget.org/packages/protobuf-net.Grpc.Native); this provies `ChannelClientFactory` which works similarly to `HttpClientFactory` above,
except instead of taking an `HttpClient`, it takes a `Channel` (the regular wrapper around the unmanaged gRPC API that `Grpc.Core` uses):

``` c#
var channel = new Channel("localhost", 10042, ChannelCredentials.Insecure);
try
{
    var calculator = channel.CreateGrpcService<ICalculator>();
    await Test(calculator);
}
finally
{
    await channel.ShutdownAsync();
}
```


At the moment the *server* isn't implemented for this API, but that's on the list of things to do.

### Summary

This is just a *very brief introduction* to what you can do with protobuf-net and gRPC using protobuf-net.Grpc; if you
want to play with it, please feel free to do so. Log issues, make suggestions, etc. Have fun.

And if this could save you a ton of time, you're always welcome to [![Buy me a coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/marcgravell)