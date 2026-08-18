# protobuf-net.Grpc.Native

[Code-first gRPC](https://grpc.protobuf-net.dev/) over the unmanaged `Grpc.Core` API, for clients and servers
that cannot use the fully managed `Grpc.Net.Client` / `Grpc.AspNetCore.Server` stack — .NET Framework, or
anything else still on the native binaries.

```csharp
var channel = new Channel("localhost", 5001, ChannelCredentials.Insecure);
var client = channel.CreateGrpcService<IMyAmazingService>();
var results = await client.SearchAsync(request);
```

A server binds an implementation to the service definitions:

```csharp
var server = new Server();
server.Services.AddCodeFirst(new MyService());
```

`Grpc.Core` itself is in maintenance; where the managed stack is an option, prefer
[protobuf-net.Grpc](https://www.nuget.org/packages/protobuf-net.Grpc) and
[protobuf-net.Grpc.AspNetCore](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore).

## More

- [Getting started](https://grpc.protobuf-net.dev/gettingstarted)
- [Package layout](https://grpc.protobuf-net.dev/projects)
- [Release notes](https://github.com/protobuf-net/protobuf-net.Grpc/releases)
