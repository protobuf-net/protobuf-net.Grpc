# Package/Project Layout

protobuf-net.Grpc is split over several projects:

## `protobuf-net.Grpc`

You will not usually reference this directly. This is the shared core and contains all the code that isn't tied to a specific client/server implementation,
and it targets all runtimes. It does not have any expensive downstream dependencies and is entirely managed. It contains all of the code related to clients (both
the managed and unmanaged client APIs share a common `ChannelBase` abstraction).

## `protobuf-net.Grpc.AspNetCore`

This is for using gRPC as a **server** with the ASP.NET Core 3 implementation. It takes a dependency on `Grpc.AspNetCore.Server` and `Microsoft.AspNetCore.App`
(although you'll already have the latter if you're hosting in ASP.NET Core). It only works on .NET Core 3 (or above).

## `protobuf-net.Grpc.Native`

This is for using gRPC as a **client or server** using the unmanaged/native binaries via `Grpc.Core` (specifically, the `Channel` API).
Like `protobuf-net.Grpc`, it works on .NET Standard 2.0 or .NET Framework 4.6.1 (or above).

(caveat: server API not yet implemented for `protobuf-net.Grpc.Native`)