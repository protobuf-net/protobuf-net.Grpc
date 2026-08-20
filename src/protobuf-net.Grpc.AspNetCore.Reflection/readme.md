# protobuf-net.Grpc.AspNetCore.Reflection

Adds the standard gRPC server reflection service to an ASP.NET Core host running
[code-first gRPC](https://grpc.protobuf-net.dev/) services, so tools like `grpcurl` and Postman can discover
the services and their schemas — which code-first services would otherwise not publish anywhere.

```csharp
builder.Services.AddCodeFirstGrpcReflection();
// ...
app.MapCodeFirstGrpcReflectionService();
```

The schemas are generated from the contracts by
[protobuf-net.Grpc.Reflection](https://www.nuget.org/packages/protobuf-net.Grpc.Reflection); nothing needs to
be written by hand or checked in.

## More

- [Creating a proto file](https://grpc.protobuf-net.dev/createProtoFile)
- [Getting started](https://grpc.protobuf-net.dev/gettingstarted)
- [Release notes](https://github.com/protobuf-net/protobuf-net.Grpc/releases)
