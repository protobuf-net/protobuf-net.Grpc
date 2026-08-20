# protobuf-net.Grpc.Reflection

Produces `.proto` schemas from [code-first gRPC](https://grpc.protobuf-net.dev/) contracts, so a code-first
service can still hand callers on other platforms the schema they need:

```csharp
var schema = new SchemaGenerator().GetSchema<IMyAmazingService>();
```

It also implements the standard gRPC server reflection service over those generated descriptors. To expose
that from an ASP.NET Core host, use
[protobuf-net.Grpc.AspNetCore.Reflection](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore.Reflection),
which wires it into the endpoint routing for you.

## More

- [Creating a proto file](https://grpc.protobuf-net.dev/createProtoFile)
- [Documentation](https://grpc.protobuf-net.dev/)
- [Release notes](https://github.com/protobuf-net/protobuf-net.Grpc/releases)
