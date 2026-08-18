# protobuf-net.Grpc.ClientFactory

Registers [code-first gRPC](https://grpc.protobuf-net.dev/) clients with dependency injection, on top of
`Grpc.Net.ClientFactory` — so the service contract interface is injectable, and gets `HttpClientFactory`'s
handler lifetime, logging and resilience along with it.

```csharp
builder.Services.AddCodeFirstGrpcClient<IMyAmazingService>(options =>
{
    options.Address = new Uri("https://localhost:5001");
});
```

This works like the built-in
[`AddGrpcClient<T>(...)`](https://learn.microsoft.com/aspnet/core/grpc/clientfactory), but additionally
configures the service for protobuf-net.Grpc's code-first style. Then take a dependency on the contract
itself — or on `GrpcClientFactory`, to resolve services yourself:

```csharp
public class MyController(IMyAmazingService service) { /* ... */ }
```

`ConfigureCodeFirstGrpcClient<T>` does the same for a client builder you already have.

## Using a custom protobuf-net model

The default/shared configuration is used unless you register your own. For *clients* the key service is
`ClientFactory`; for *servers* it is `BinderConfiguration`. Both can come from one model:

```csharp
var model = RuntimeTypeModel.Create(); // configure the protobuf-net model as needed

var marshallerFactory = ProtoBufMarshallerFactory.Create(model, ProtoBufMarshallerFactory.Options.None);
var binderConfiguration = BinderConfiguration.Create([marshallerFactory]);

services.AddSingleton(binderConfiguration)
        .AddSingleton(ClientFactory.Create(binderConfiguration));
```

## More

- [Registering a client service](https://grpc.protobuf-net.dev/registerClientService)
- [Configuration options](https://grpc.protobuf-net.dev/configuration)
- [Release notes](https://github.com/protobuf-net/protobuf-net.Grpc/releases)
