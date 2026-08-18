# protobuf-net.Grpc.AspNetCore

Hosts [code-first gRPC](https://grpc.protobuf-net.dev/) services in ASP.NET Core: your service is a class
implementing a `[ServiceContract]` interface, with no `.proto` file and no generated base class.

```csharp
builder.Services.AddCodeFirstGrpc();
// ...
app.MapGrpcService<MyService>();
```

```csharp
public class MyService : IMyAmazingService
{
    public ValueTask<SearchResponse> SearchAsync(SearchRequest request) { /* ... */ }
}
```

Everything else is ordinary `Grpc.AspNetCore.Server` — interceptors, health checks, logging and the rest all
apply unchanged. For a `.proto` schema, or gRPC server reflection, add
[protobuf-net.Grpc.AspNetCore.Reflection](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore.Reflection).

## More

- [Getting started](https://grpc.protobuf-net.dev/gettingstarted)
- [Configuration options](https://grpc.protobuf-net.dev/configuration)
- [Release notes](https://github.com/protobuf-net/protobuf-net.Grpc/releases)
