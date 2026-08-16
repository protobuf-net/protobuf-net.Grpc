# Native AOT and trimming

> **Preview.** The attributes described here are marked `[Experimental]` with the id **`PBN9001`**,
> which is a compile *error* until you suppress it — see [Opting in](#opting-in), or
> [the PBN9001 page](https://docs.protobuf-net.dev/exp/PBN9001), which covers both halves. The shape
> may still change.

Normally, protobuf-net.Grpc works out how to talk to a service at *runtime*: it reflects over your
`[Service]` interface and emits IL for a client proxy, and builds server bindings with
`Expression.Compile`. That is flexible, and it is fundamentally incompatible with **native AOT**,
where there is no IL emitter, and awkward under **trimming**, where the members it wants to reflect
over may already have been removed.

`protobuf-net.BuildTools` can build both at **compile time** instead, as ordinary C# in your own
project — code you can read, step through, and that ILC can compile like anything else.

## Two halves, and you need both

This is the part that catches people out, so it is worth stating plainly.

Generated proxies still have to turn your request and response types into bytes, and by default that
goes through `RuntimeTypeModel.Default` — which builds serializers by **reflection**. So it is
entirely possible to have proxies that are perfectly AOT-safe carrying payloads that are not. The
build succeeds, everything works under JIT, and the failure arrives at publish time.

So you need:

1. a **serializer model**, from protobuf-net's `[ProtoModel]` — see
   [the protobuf-net AOT documentation](https://docs.protobuf-net.dev/aot);
2. a **gRPC model**, from `[ProtoGrpc]`, *pointed at that serializer model*.

## Opting in

Declare a partial class deriving from `ClientFactory`, and tell it what to generate:

``` c#
using ProtoBuf.Grpc.Configuration;

[ProtoGrpc(Model = typeof(MyModel))]
[ProtoService(typeof(IGreeter), typeof(GreeterService))]
public sealed partial class MyServices : ClientFactory { }
```

- `Model` names your `[ProtoModel]` type. **Omit it and the payloads still go through the reflective
  runtime model**, which is reported as a warning.
- `[ProtoService]` names a contract. The second argument is the implementation, and is only needed in
  the project that *hosts* the service — naming it is what lets the generated server bindings resolve
  everything at compile time.

Both attributes are `[Experimental]`, so you must suppress `PBN9001`. The same id covers
protobuf-net's `[ProtoModel]`, so one entry does both halves:

``` xml
<PropertyGroup>
  <NoWarn>$(NoWarn);PBN9001</NoWarn>
</PropertyGroup>
```

## Using it

On the client, name the factory — that is what selects the generated proxy over the ref-emit one:

``` c#
var greeter = channel.CreateGrpcService<IGreeter>(MyServices.Instance);
```

On the server, use the registration method generated into your own assembly, **instead of**
`AddCodeFirstGrpc()`:

``` c#
builder.Services.AddMyServices();       // generated; named after your type
app.MapGrpcService<GreeterService>();
```

`AddCodeFirstGrpc()` registers the reflection-based binder, so calling both would bind every
operation twice. The generated method registers only what was generated.

## Diagnostics

Anything the generator cannot handle is reported rather than guessed at, and everything it reports is
a **warning**: an incomplete set of proxies still builds, and still works under JIT. Escalate them if
you want the model to be complete or the build to fail:

``` xml
<WarningsAsErrors>$(WarningsAsErrors);PBN4010</WarningsAsErrors>
```

| id | meaning |
| --- | --- |
| `PBN4000` | the language version is too low for build-time proxies |
| `PBN4001` | the service interface is nested |
| `PBN4002` | a method shape is not emitted at build time, so the contract is left to the runtime |
| `PBN4003` | the service interface is generic |
| `PBN4004` | a base interface is not marked `[SubService]` |
| `PBN4005` | the `[ProtoGrpc]` type is not `partial` |
| `PBN4006` | the `[ProtoGrpc]` type does not derive from `ClientFactory` |
| `PBN4007` | a type named by `[ProtoService]` is not a service contract |
| `PBN4008` | a named implementation does not implement its contract |
| `PBN4010` | **no serializer model was named**, so payloads will be marshalled reflectively |
| `PBN4011` | a named contract could not be resolved |

`PBN4010` is the one to pay attention to: it is the "two halves" problem above.

## Checking it really works

Publish for native AOT and *run* it. Everything else runs on a JIT runtime where the reflection path
still exists, so it can hide a problem that only appears once ILC has trimmed:

``` sh
dotnet publish -c Release -r win-x64
```

`<PublishAot>true</PublishAot>` also enables trim/AOT analysis at ordinary *build* time, so you can
see the warnings without paying for a native publish.
