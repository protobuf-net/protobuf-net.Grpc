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

Both can be built at **compile time** instead, as ordinary C# in your own project — code you can read,
step through, and that ILC can compile like anything else.

## What you need

- **protobuf-net 3.4.0 or later.** The generators ship with it; there is nothing extra to reference.
- **protobuf-net.Grpc 1.3.6 or later.** Earlier versions statically root `RuntimeTypeModel.Default`,
  which keeps the whole reflection path reachable however static your own code is — on our own smoke
  test that difference is 100 trim warnings versus 4.

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

You do **not** have to populate the serializer model by hand, though — see
[The payload types come for free](#the-payload-types-come-for-free).

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

If you want none of this — no analyzers, no generators, from either half — one property turns all of
protobuf-net's build-time tooling off, and is checked before any work happens:

``` xml
<ProtoBufDisableBuildTools>true</ProtoBufDisableBuildTools>
```

## The payload types come for free

`[ProtoService]` already names your contracts, so the serializer model does not need to be told about
their request and response types a second time — they are added to it automatically. A `[ProtoModel]`
that exists only to serve gRPC therefore needs no `[ProtoSerializable]` at all:

``` c#
[ProtoModel]
public partial class MyModel : TypeModel { }        // that is the whole declaration
```

Add `[ProtoSerializable]` only for types you serialize directly yourself, outside gRPC.

If the model lives in a **referenced assembly**, nothing can be added to it from here, so instead it is
*checked*: any payload it has no serializer for is reported (`PBN4013`), naming the type and the model.

## Using it: clients

There are two ways to point a client at the generated proxies, and the one to reach for first needs no
change to your calling code at all.

### Recommended: let interceptors do it

Tell the compiler that protobuf-net's interceptors are enabled — once, in your project file. The
namespace is fixed; this is consent, not configuration:

``` xml
<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);ProtoBuf.AOT</InterceptorsNamespaces>
</PropertyGroup>
```

Now ordinary code gets the generated proxy, and existing call sites need no edit:

``` c#
var greeter = channel.CreateGrpcService<IGreeter>();
```

Four things worth knowing, because "the compiler rewrote my call" deserves them:

- **it only ever swaps the factory argument.** The rewritten call is exactly
  `(clientFactory ?? MyServices.Instance).CreateClient<TService>(client)` — the same thing you would
  have written by hand, so there is no behaviour hiding in it.
- **a call that already passes a factory is left alone.** Yours wins.
- **it is opt-in because it has to be.** An interceptor in a namespace you have not enabled is a
  compile *error* (`CS9137`), so nothing is generated unless you ask for it.
- **it needs the .NET 9 SDK or later.** That is where the compiler API this relies on arrived. Older
  toolchains used `<InterceptorsPreviewNamespaces>` instead, and that spelling is honoured too.

### Or name the factory yourself

If you would rather not enable interceptors — or cannot — pass the factory explicitly. This is
precisely what the interceptor does for you, so the two produce the same program:

``` c#
var greeter = channel.CreateGrpcService<IGreeter>(MyServices.Instance);
```

Naming the factory is what selects the generated proxy over the ref-emit one. If you have generated
proxies for a contract and a call is not using them, `PBN4016` says so, and offers a code fix that
inserts exactly the argument above.

### Clients registered through dependency injection

`AddCodeFirstGrpcClient<T>` does not take a factory — it resolves one from the container:

``` c#
services.AddSingleton<ClientFactory>(MyServices.Instance);   // once, anywhere in your setup
services.AddCodeFirstGrpcClient<IGreeter>();                 // ...and every client uses it
```

The generated `AddMyServices()` does that registration for you, so a project that hosts services *and*
consumes them needs nothing extra. A client-only project has no `AddMyServices()` to call, and is
reminded with `PBN4017`.

## Using it: servers

Use the registration method generated into your own assembly, **instead of** `AddCodeFirstGrpc()`:

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
| `PBN4003` | the service interface is an **open** generic; a closed construction is fine |
| `PBN4004` | a base interface is not marked `[SubService]` |
| `PBN4005` | the `[ProtoGrpc]` type is not `partial` |
| `PBN4006` | the `[ProtoGrpc]` type does not derive from `ClientFactory` |
| `PBN4007` | a type named by `[ProtoService]` is not a service contract |
| `PBN4008` | a named implementation does not implement its contract |
| `PBN4009` | a contract declares no methods recognised as operations |
| `PBN4010` | **no serializer model was named**, so payloads will be marshalled reflectively |
| `PBN4011` | a named contract could not be resolved |
| `PBN4012` | the named model is in this project but is not a `[ProtoModel]` |
| `PBN4013` | a model in a *referenced* assembly has no serializer for a payload type |
| `PBN4014` | the `[ProtoGrpc]` type is nested or generic |
| `PBN4015` | this project publishes AOT or trimmed and has no `[ProtoGrpc]` at all |
| `PBN4016` | a call is not using the proxies you have generated — **has a code fix** |
| `PBN4017` | DI-registered clients are not using them — see [above](#clients-registered-through-dependency-injection) |
| `PBN4018` | a contract was dropped, and under AOT that means calls on it will **throw** |

Two to pay particular attention to:

- **`PBN4010`** is the "two halves" problem above.
- **`PBN4018`** appears only when you have asked for AOT or trimming, and it is the difference between
  "this contract falls back to reflection" and "this contract does not work". On a JIT build a dropped
  contract really does keep working; under AOT there is no runtime proxy to fall back to.

## What is supported

The code-first surface protobuf-net.Grpc itself binds: unary, client-streaming, server-streaming and
duplex operations; `CallContext` or `CancellationToken`; void requests and responses; `[SubService]`
bases; `IDisposable`/`IAsyncDisposable`; `[Operation]` naming and the WCF
`[ServiceContract]`/`[OperationContract]` equivalents; and closed generic contracts.

Shapes that stay on the runtime path — `IObservable<T>`, `Stream`, `Grpc.Core`'s own call types, a base
interface that is not `[SubService]` — are each reported, and under AOT that report is `PBN4018`:
there is no runtime path there, so those contracts will throw rather than degrade.

## Checking it really works

Publish for native AOT and *run* it. Everything else runs on a JIT runtime where the reflection path
still exists, so it can hide a problem that only appears once ILC has trimmed:

``` sh
dotnet publish -c Release -r win-x64
```

`<PublishAot>true</PublishAot>` also enables trim/AOT analysis at ordinary *build* time, so you can
see the warnings without paying for a native publish.

For what it is worth, that is how this is tested: a real client and a real server, over a real socket,
in a single natively-compiled binary, exercising all five operation shapes plus interception — because
a JIT run cannot tell you whether any of this actually works.
