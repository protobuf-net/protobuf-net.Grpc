# protobuf-net.Grpc

Code-first gRPC for .NET: describe the service as an interface, implement it, and consume it — no `.proto`
file and no generated client in between.

```csharp
[ServiceContract]
public interface IMyAmazingService
{
    ValueTask<SearchResponse> SearchAsync(SearchRequest request);
}
```

The server implements that interface; the client asks for it:

```csharp
var client = http.CreateGrpcService<IMyAmazingService>();
var results = await client.SearchAsync(request);
```

The messages are ordinary [protobuf-net](https://www.nuget.org/packages/protobuf-net) contracts, so the wire
format is plain protobuf and other gRPC implementations can talk to it — with a `.proto` schema generated on
demand if they need one.

This package is the shared core, and is what a **client** over `Grpc.Net.Client` needs. Most other scenarios
want one of:

- [protobuf-net.Grpc.AspNetCore](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore) — servers on ASP.NET Core
- [protobuf-net.Grpc.ClientFactory](https://www.nuget.org/packages/protobuf-net.Grpc.ClientFactory) — clients via dependency injection
- [protobuf-net.Grpc.Native](https://www.nuget.org/packages/protobuf-net.Grpc.Native) — the unmanaged `Grpc.Core` API

## More

- [Getting started](https://grpc.protobuf-net.dev/gettingstarted)
- [Documentation](https://grpc.protobuf-net.dev/)
- [Release notes](https://github.com/protobuf-net/protobuf-net.Grpc/releases)
