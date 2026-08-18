# Package/Project Layout

protobuf-net.Grpc is split over several packages; you generally reference the one that matches how you are
hosting or consuming the service.

## `protobuf-net.Grpc`

The shared core: everything that is not tied to a specific client/server implementation. It targets all
runtimes, is entirely managed, and has no expensive downstream dependencies. All the client-side code lives
here — the managed and unmanaged client APIs share a common `ChannelBase` abstraction — so a client over
`Grpc.Net.Client` needs only this package.

## `protobuf-net.Grpc.AspNetCore`

For using gRPC as a **server** on ASP.NET Core. It takes a dependency on `Grpc.AspNetCore.Server` and
`Microsoft.AspNetCore.App` (which you already have if you are hosting in ASP.NET Core).

## `protobuf-net.Grpc.ClientFactory`

For resolving **clients** from dependency injection, on top of `Grpc.Net.ClientFactory`: the service contract
interface becomes injectable, with `HttpClientFactory`'s handler lifetime and logging applied to it.

## `protobuf-net.Grpc.Reflection`

Generates `.proto` schemas from code-first contracts, and implements the gRPC reflection service over them.
Useful on its own for producing a schema to hand to callers on other platforms.

## `protobuf-net.Grpc.AspNetCore.Reflection`

Wires the above into ASP.NET Core endpoint routing, so tools such as `grpcurl` can discover the services a
code-first host exposes.

## `protobuf-net.Grpc.Native`

For using gRPC as a **client or server** over the unmanaged/native binaries via `Grpc.Core` (specifically,
the `Channel` API). Like `protobuf-net.Grpc`, it works on .NET Standard 2.0 and .NET Framework. `Grpc.Core`
itself is in maintenance, so prefer the managed stack where that is an option.
