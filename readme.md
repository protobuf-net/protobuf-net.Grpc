# <img src="https://protogen.marcgravell.com/images/protobuf-net.svg" alt="protobuf-net logo" width="45" height="45"> protobuf-net.Grpc

[![Build status](https://ci.appveyor.com/api/projects/status/en9i5mp471ci6ip3/branch/main?svg=true)](https://ci.appveyor.com/project/StackExchange/protobuf-net-grpc/branch/main)

`protobuf-net.Grpc` adds code-first support for services over gRPC using either the native `Grpc.Core` API, or the fully-managed `Grpc.Net.Client` / `Grpc.AspNetCore.Server` API.

It should work on all .NET languages that can generate something *even remotely like* a regular .NET type model.

- [Getting Started](https://protobuf-net.github.io/protobuf-net.Grpc/gettingstarted)
- [All Documentation](https://protobuf-net.github.io/protobuf-net.Grpc/)
- [Build/usage available via `protobuf-net.BuildTools`](https://protobuf-net.github.io/protobuf-net/build_tools)

Usage is as simple as declaring an interface for your service-contract:

``` c#
[ServiceContract]
public interface IMyAmazingService {
    ValueTask<SearchResponse> SearchAsync(SearchRequest request);
    // ...
}
```

then either implementing that interface for a server:

``` c#
public class MyServer : IMyAmazingService {
    // ...
}
```

or asking the system for a client:

``` c#
var client = http.CreateGrpcService<IMyAmazingService>();
var results = await client.SearchAsync(request);
```

This would be equivalent to the service in .proto:

``` proto
service MyAmazingService {
    rpc Search (SearchRequest) returns (SearchResponse) {}
	// ...
}
```

Obviously you need to tell it the uri etc - see [Getting Started](https://protobuf-net.github.io/protobuf-net.Grpc/gettingstarted). Usually the configuration is convention-based, but
if you prefer: there are [various configuration options](https://protobuf-net.github.io/protobuf-net.Grpc/configuration).

## Getting hold of it

Everything is available as pre-built packages on nuget; in particular, you probably want one of:

- [`protobuf-net.Grpc.AspNetCore`](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore) for servers using ASP.NET Core 3.1
- [`protobuf-net.Grpc.Native`](https://www.nuget.org/packages/protobuf-net.Grpc.Native) for clients or servers using the native/unmanaged API
- [`protobuf-net.Grpc`](https://www.nuget.org/packages/protobuf-net.Grpc) and [`Grpc.Net.Client`](https://www.nuget.org/packages/Grpc.Net.Client/) for clients using `HttpClient` on .NET Core 3.1

`protobuf-net.Grpc` also ships a Roslyn source generator inside the same package (under `analyzers/dotnet/cs/`) that emits build-time client proxies and server bindings — see [Trimming and AOT](#trimming-and-aot) below.

[Usage examples are available in C#, VB and F#](https://github.com/protobuf-net/protobuf-net.Grpc/tree/main/examples/pb-net-grpc).

## Trimming and AOT

The source generator runs automatically wherever `protobuf-net.Grpc` is referenced — no extra package, no opt-in attribute, no `partial` keyword on your interfaces. For every `[Service]` / `[ServiceContract]` interface declared in your project, the generator emits both a client proxy and server bindings in the `ProtoBuf.Grpc.Generated.*` namespace, registered via a `[ModuleInitializer]`.

`protobuf-net.Grpc.dll` itself is fully trim-clean — the runtime `Reflection.Emit` and `Expression.Compile` paths are gated behind `RuntimeFeature.IsDynamicCodeSupported` and annotated `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]`, so the trimmer can shake them away.

**However**, the default serializer (`protobuf-net`) is still reflection-based — `RuntimeTypeModel.Default.CanSerialize(typeof(T))` walks `T`'s members at runtime to discover `[ProtoContract]` / `[ProtoMember]`. Under `PublishTrimmed=true`, you currently need to root the assemblies whose contract types you want to serialize:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="MyApp.Contracts" />
  <TrimmerRootAssembly Include="protobuf-net" />
  <TrimmerRootAssembly Include="protobuf-net.Core" />
</ItemGroup>
```

Without those, the trimmer strips members and attributes that `RuntimeTypeModel` needs at discovery time, marshaller resolution returns null, and your service either fails to bind methods (by default a fail-fast at startup) or — if you set `services.Configure<CodeFirstGrpcOptions>(o => o.ContinueOnBindFailure = true)` — comes up with a partial service surface and a warning per missing method.

Under `PublishAot=true` the serializer's reflection breaks more fundamentally — the only path for full AOT today is to switch the marshaller layer to a generator-based serializer (Google.Protobuf, MemoryPack, MessagePack-CSharp via a custom `MarshallerFactory`).

## Anything else?

`protobuf-net.Grpc` is created and maintained by [Marc Gravell](https://github.com/mgravell) ([@marcgravell](https://twitter.com/marcgravell)), the author of `protobuf-net`.

It makes use of tools from [grpc](https://github.com/grpc/), but is not official associated with, affiliated with, or endorsed by that project.

I look forward to your feedback, and if this could save you a ton of time, you're always welcome to [![Buy me a coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/marcgravell)