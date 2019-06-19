# protobuf-net.Grpc

`protobuf-net.Grpc` adds code-first support for services over gRPC using either the native `Grpc.Core` API, or the fully-managed `Grpc.Net.Client` / `Grpc.AspNetCore.Server` API.

Usage is as simple as declaring an interface for your service-contract:

``` c#
public interface IMyAmazingService {
    ValueTask<SearchResponse> SearchAsync(SearchRequest request);
	// ...
}
```

then either implementing that interface for a server:

``` c#
public class MyServer : IAmazingService {...}
```

or asking the system for a client:

``` c#
var client = http.CreateGrpcService<IAmazingService>();
```

Obviously you need to tell it the uri etc - see [Getting Started](https://mgravell.github.io/protobuf-net.Grpc/gettingstarted)

## Getting hold of it

Everything is available as pre-built packages on nuget; in particular, you probably want one of:

- [`protobuf-net.Grpc.HttpClient`](https://www.nuget.org/packages/protobuf-net.Grpc.HttpClient) for clients using `HttpClient` on .NET Core 3
- [`protobuf-net.Grpc.AspNetCore`](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore) for servers using ASP.NET Core 3
- [`protobuf-net.Grpc.Native`](https://www.nuget.org/packages/protobuf-net.Grpc.Native) for clients and servers using the native `Grpc.Core` API

## That's sounds awesome!

Glad you think so! Have fun, and if this could save you a ton of time, you're always welcome to [![Buy me a coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/marcgravell)