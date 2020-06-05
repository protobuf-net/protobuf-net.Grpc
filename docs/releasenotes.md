# Release Notes

## (unreleased)

- support `CancellationToken` in service signatures in place of `CallContext` (#95)
- service discovery correctly considers method accessibility (#87)

## 1.0.75

- addition of [`ClientFactory`](https://www.nuget.org/packages/protobuf-net.Grpc.ClientFactory) support
- improvements to streaming (client/server/duplex) orchestration
- improvements to metadata (headers/trailers) collection

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