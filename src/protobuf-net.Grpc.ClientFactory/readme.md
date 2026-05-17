`protobuf-net.Grpc.ClientFactory` allows accessing clients in a standard .NET dependency-injection mechanism.

Core APIs:

``` csharp
services.AddCodeFirstGrpcClient<IMyService>(...);
```

This works like the inbuilt [`AddGrpcClient<T>(...)` API](https://learn.microsoft.com/aspnet/core/grpc/clientfactory),
but additionally configures the gRPC service for use with protobuf-net.Grpc's code-first style. In addition to
other APIs, this allows the `GrpcClientFactory` API to be injected to provide access to services.

By default, this uses the default/shared configuration. If you wish to use a bespoke protobuf-net configuration,
additional services can be injected into the service provider. For *servers*, the `BinderConfiguration` is the primary
API to inject for additional configuration. For *clients*, the `ClientFactory` is the most important. You
can (if you wish) unify this by providing both:

``` c#
var model = RuntimeTypeModel.Create(); // custom model
// not shown: configure protobuf-net model

// prepare a custom protobuf-net.Grpc configuration using that model
var marshallerFactory = ProtoBufMarshallerFactory.Create(model, ProtoBufMarshallerFactory.Options.None);
var binderConfiguration = BinderConfiguration.Create([marshallerFactory]);

// register server and client overrides
services.AddSingleton(binderConfiguration).AddSingleton(ClientFactory.Create(binderConfiguration));
```