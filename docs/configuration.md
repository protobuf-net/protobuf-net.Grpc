# Configuration Options

protobuf-net.Grpc has a few roles:

1. to identify service contracts
2. to identify operation contracts within service contracts
3. to provide marshallers for serializing/deserializing different types
4. to provide efficient mechanisms to instantiate clients and servers
5. to hook everything together into the underlying gRPC API

Taking those in turn, then:

## 1 Service Contracts

By default, the library expects service contracts to be decorated with `[ServiceContract]` (using this for the name when possible); however, if this doesn't work for you,
you can implement a custom `ServiceBinder`, and override the `IsServiceContract` method; this lets you a: decide whether to include
it, and b: specify the name.

## 2 Operation Contracts

By default, the library considers all methods it can feasibly handle (optionally using `[OperationContract]` for the name). If you want to change this,
again implement a custom `ServiceBinder`, and override the `IsOperationContract` method; this lets you a: decide whether to include
it, and b: specify the name.

## 3 Marshallers

By default, the library uses protobuf-net's default model (`RuntimeTypeModel.Default`). If you want to use a *configured* protobuf-net model, you can
create a `ProtoBufMarshallerFactory`, specifying the model you wish to use. However, you don't need to use protobuf-net *at all*; you can also implement
a custom `MarshallerFactory` from scratch. The *easiest* way to do this is to override the `Serialize<T>` and `Deserialize<T>` methods, but if you need
to do something more sophisticated, you can override `CreateMarshaller<T>`. You will also need to override `CanSerialize`, which is used to determine
whether a type can be serialized. Note: you should ensure that your payload is protobuf - otherwise your service won't be usable by other clients.

## 4 Creating clients and servers

All of the configuration options above are bundled into a `BinderConfiguration` instance. Anything that you don't supply will use the default behavior.
Once you have a configuration, you need to be able to use it!

- for servers using `protobuf-net.Grpc.Native`, you can pass the binder-configuration to `AddCodeFirst` when registering your service
- for servers using `protobuf-net.Grpc.AspNetCore`, you can supply the binder-configuration in `ConfigureServices`, via `services.AddSingleton`
- for clients, you will need to create a `ClientFactory` instance using the binder-configuration - noting that a `ClientFactory` instance should be considered expensive
and should be stored and re-used between requests; this can then be supplied to the `CreateGrpcService` method when creating client instances

## 5 How bindings happen

There are currently no controllable options for bindings.

If you're wondering why I dind't say how to configure TLS, authentication, etc: that's because it *isn't a concern of `protobuf-net.Grpc`*. All the
expected options are available - they're just not something that this library sees or controls; rather, you'd do that at the server in whatever way
is normal for that server. For clients, authentication can be specified via `CallOptions` (the standard `Grpc.Core.Api` type). `protobuf-net.Grpc` *unifies*
the `CallOptions` and `ServerCallContext` types into a single value-type: `CallContext`. It is common to include an optional `CallContext` parameter
on your methods for this purpose, i.e.

``` c#
[ServiceContract]
public interface IMyAmazingService {
    ValueTask<SearchResponse> SearchAsync(SearchRequest request, CallContext context = default);
    // ...
}
```

The client can now provide this additional detail by passing in a `CallContext` with the `CallOptions` that describe the needs. `CallContext` is implicitly
convertible from `CallOptions`, so you don't need to do the intermediate step:

``` c#
var client = http.CreateGrpcService<IMyAmazingService>();
var options = new CallOptions(...); // your gRPC options here including auth if needed
var result = await client.Search(request, options);
```