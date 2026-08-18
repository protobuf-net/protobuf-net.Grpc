# <img src="https://raw.githubusercontent.com/protobuf-net/protobuf-net/main/protobuf-net-48.png" alt="protobuf-net logo" width="45" height="45"> protobuf-net.Grpc

[![.NET](https://github.com/protobuf-net/protobuf-net.Grpc/actions/workflows/dotnet.yml/badge.svg)](https://github.com/protobuf-net/protobuf-net.Grpc/actions/workflows/dotnet.yml)

`protobuf-net.Grpc` adds code-first support for services over gRPC using either the native `Grpc.Core` API, or the fully-managed `Grpc.Net.Client` / `Grpc.AspNetCore.Server` API.

It should work on all .NET languages that can generate something *even remotely like* a regular .NET type model.

- [Getting Started](https://grpc.protobuf-net.dev/gettingstarted)
- [All Documentation](https://grpc.protobuf-net.dev/)
- [Build/usage available via `protobuf-net.BuildTools`](https://docs.protobuf-net.dev/build_tools)

Usage is as simple as declaring an interface for your service-contract:

``` c#
[ServiceContract]
public interface IMyAmazingService {
    ValueTask<SearchResponse> SearchAsync(SearchRequest request);
    // ...
}
```

then either implementing that interface for a server:

``` c#
public class MyServer : IMyAmazingService {
    // ...
}
```

or asking the system for a client:

``` c#
var client = http.CreateGrpcService<IMyAmazingService>();
var results = await client.SearchAsync(request);
```

This would be equivalent to the service in .proto:

``` proto
service MyAmazingService {
    rpc Search (SearchRequest) returns (SearchResponse) {}
	// ...
}
```

Obviously you need to tell it the uri etc - see [Getting Started](https://grpc.protobuf-net.dev/gettingstarted). Usually the configuration is convention-based, but
if you prefer: there are [various configuration options](https://grpc.protobuf-net.dev/configuration).

## Getting hold of it

Everything is available as pre-built packages on nuget; in particular, you probably want one of:

- [`protobuf-net.Grpc.AspNetCore`](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore) for servers using ASP.NET Core
- [`protobuf-net.Grpc.ClientFactory`](https://www.nuget.org/packages/protobuf-net.Grpc.ClientFactory) for clients resolved from dependency injection
- [`protobuf-net.Grpc`](https://www.nuget.org/packages/protobuf-net.Grpc) and [`Grpc.Net.Client`](https://www.nuget.org/packages/Grpc.Net.Client/) for clients using `HttpClient`
- [`protobuf-net.Grpc.Native`](https://www.nuget.org/packages/protobuf-net.Grpc.Native) for clients or servers using the native/unmanaged API

[What each package is for](https://grpc.protobuf-net.dev/projects).

[Usage examples are available in C#, VB and F#](https://github.com/protobuf-net/protobuf-net.Grpc/tree/main/examples/pb-net-grpc).

## Anything else?

`protobuf-net.Grpc` is created and maintained by [Marc Gravell](https://github.com/mgravell) ([@marcgravell](https://twitter.com/marcgravell)), the author of `protobuf-net`.

It makes use of tools from [grpc](https://github.com/grpc/), but is not official associated with, affiliated with, or endorsed by that project.

I look forward to your feedback, and if this could save you a ton of time, you're always welcome to [![Buy me a coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/marcgravell)