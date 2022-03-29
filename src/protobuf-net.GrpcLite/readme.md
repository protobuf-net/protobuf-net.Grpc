# What is protobuf-net.GrpcLite?

It is a drop in protocol replacement for gRPC; the .NET gRPC API has no hard bindings to either the marshaller or the underlying transport; protobuf-net.Grpc offered ways to change the marshaller (for example,
allowing you to use protobuf-net) - and now protobuf-net.GrpcLite allows you to change the transport - from HTTP/2 to a custom transport *inspired* by HTTP/2, but simpler and with lower overheads. It is also
fully managed, unlike HTTP/2 which often required unmanaged library or OS support.

The transport *is not compatible* with regular HTTP/2 gRPC, but: all of your existing gRPC code should continue to function, as long as you have a client and server that can talk the same dialect.

## How do I use it?

At the client, instead of using `var channel = new Channel(...);` (unmanaged HTTP/2) or `var channel = GrpcChannel.ForAddress(...);` (managed HTTP/2), you would use something like:

``` c#
using var channel = await ConnectionFactory.ConnectSocket(endPoint).AsFrames().CreateChannelAsync();
```

The rest of your client code *shouldn't change at all*. This is just one example; other terminators are possible - for example, anything that can provide a `Stream` should work, including support
for things like TLS, compression, named pipes, etc.

At the server, the code is currently a bit closer to the unmanaged server implementation (the server does not integrate deeply into Kestrel, although it works fine inside a Kestrel process); for example:

``` c#
var server = new LiteServer();
server.Bind(new MyService());
_ = server.ListenAsync(ConnectionFactory.ListenSocket(endpoint).AsStream().AsFrames());
// ... note: leave your server running here, until you're ready to exit!
server.Stop();
```

The `ListenAsync` call will listen for multiple connections; a single server can listen to many connections on many different listeneres at once - for example, you could
listen to multiple TCP ports, with/without TLS. Your `MyService` instance will be activated just like it would have been with the unmanaged server host.

## Other notes

It currently targets .NET Framework 4.7.2 up to .NET 6.0, using newer features when available. It is still very experimental - but most core things should work; feedback is welcome.

Known gaps:

- gRPC auth (although transport auth works fine)
- per-stream service activation (rather than singleton)
- testing needs more coverage
- per-stream backoff negotiation; designed, not yet implemented
- for some reason the server implementation isn't working 100% with SAEA currently - hence `.AsStream().AsFrames()` instead of just `.AsFrames()`