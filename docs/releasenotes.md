# Release Notes

## 1.1.1

- update library references
- drop net461 (moving to net462 as lower bound); drop netcoreapp3.1 and net5.0 (moving to net6.0/netstandard2.1 as lower bound)
- improve handling of `IDisposable` and delegates in services (#199 by lanfeust69)

## 1.0.177

- support sub`[SubService]` (imports one interface inside another top-level service interface) (#206 via meirkr; also protobuf-net #859)
- automatically resolve Google.Protobuf `IMessage` types used in APIs

## 1.0.171

- try to improve blazor linker support (i.e. avoid removal of necessary APIs)

## 1.0.136

- add .NET 5 target
- update gRPC etc dependencies
- add `CallContext.ResponseHeadersAsync()` (now preferred) to allow async await for headers
- allow `CreateGrpcService` to be used as an extension method from `CallInvoker`
- WCF migration samples added (#135 via mholo65)

## 1.0.110

- add support for server-side reflection (think "mex"/"wsdl") in `protobuf-net.Grpc.AspNetCore.Reflection` (#49/#63 via mholo65)
- add .proto schema generation tools in `protobuf-net.Grpc.Reflection`
- add `[SimpleRpcExceptions]` (which can be applied at service contract, service type, or service method levels), and `SimpleRpcExceptionsInterceptor` (which can be applied to any service registration) - which expose server exceptions more conveniently (#75)
- use linker-friendly metadata inspection (#90)
- add non-generic `AddCodeFirst` native server overload (#106)

## 1.0.90

- make use of the contextual (buffer rather than `byte[]`) APIs when possible (i.e. when protobuf-net v3 is targeted)

## 1.0.81

- support `CancellationToken` in service signatures in place of `CallContext` (#95)
- service discovery correctly considers method accessibility (#87)

## 1.0.75

- addition of [`ClientFactory`](https://www.nuget.org/packages/protobuf-net.Grpc.ClientFactory) support
- improvements to streaming (client/server/duplex) orchestration
- improvements to metadata (headers/trailers) collection
- update gRPC references

## 1.0.21 - 1.0.37

- (untracked)

## 1.0.13

- add API to allow additional configuration when calling `AddCodeFirstGrpc`

## 1.0.10

- add alternative API for defining simple services ([via @mythz](https://github.com/protobuf-net/protobuf-net.Grpc/pull/23))

## 1.0.6

- fix type creation on .NET Standard (thanks @imsh)
- fix break in underlying gRPC API (removal of `LiteClientBase`)
- add net461 TFM

## 1.0

- Released targeting .NET Core 3 RTM