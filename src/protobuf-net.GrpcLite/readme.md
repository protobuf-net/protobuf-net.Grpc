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

At the server, the code is currently a bit closer to the unmanaged server implementation (the server does not integrate deeply into Kestrel, although it works fine inside a Kestrel process); service-binding
is via the `.ServiceBinder`:

``` c#
var server = new LiteServer();
server.ServiceBinder.Bind(new MyService()); // contract-first example, generated via protoc

// alternative if not also using protobuf-net.Grpc, which provides the Bind API
// YourService.BindService(server.ServiceBinder, new MyService());

_ = server.ListenAsync(ConnectionFactory.ListenSocket(endpoint).AsStream().AsFrames());
// ... note: leave your server running here, until you're ready to exit!
server.Stop();
```

The `ListenAsync` call will listen for multiple connections; a single server can listen to many connections on many different listeneres at once - for example, you could
listen to multiple TCP ports, with/without TLS. Your `MyService` instance will be activated just like it would have been with the unmanaged server host.

## How do I use TLS?

TLS is provided via `SslStream`, and works with or without client certificates; the `WithTls()` connector optionally accepts callbacks for providing user certificates (client), or
validating remote certificates (client or server); the `AuthenticateAsServer()` connector accepts a server certificate, and optionally demands client certificates; for example:

``` c#
// TCP server; no TLS
_ = server.ListenAsync(ConnectionFactory.ListenSocket(endpoint).AsStream().AsFrames());
// TCP server; TLS, no client certs
_ = server.ListenAsync(ConnectionFactory.ListenSocket(endpoint).AsStream().WithTls().AuthenticateAsServer(serverCert).AsFrames());
// TCP server; TLS, client certs (validated via userCheck)
_ = server.ListenAsync(ConnectionFactory.ListenSocket(endpoint).AsStream().WithTls(userCheck).AuthenticateAsServer(serverCert, clientCertificateRequired: true).AsFrames());


``` c#
// TCP client; no TLS
using var channel = await ConnectionFactory.ConnectSocket(endPoint).AsFrames().CreateChannelAsync();
// TCP client; TLS, using default server validation and certificate selection
using var channel = await ConnectionFactory.ConnectSocket(endpoint).AsStream().WithTls().AuthenticateAsClient("mytestserver").AsFrames().CreateChannelAsync();
// TCP client; TLS, using custom server validation and certificate selection
using var channel = await ConnectionFactory.ConnectSocket(endpoint).AsStream().WithTls(serverCheck, certSelector).AuthenticateAsClient("mytestserver").AsFrames().CreateChannelAsync();
```

## How do I use code-first?

At the client, code-first works exactly as it always has; just use the `.CreateClient<TService>()` method on the channel.

As the server, binding code-first serves to the custom server is uses the `.Binder` API:

``` c#
server.ServiceBinder.AddCodeFirst(...);
```

## How do I use interceptors?

Client-side interceptors work exactly like they do in all scenarios.

To register a server-side interceptor, the `Intercept()` API is used alongside the `.ServiceBinder`:

``` c#
server.ServiceBinder.Intercept(...).Bind(new MyService()); // contract-first example, generated via protoc
server.ServiceBinder.Intercept(...).AddCodeFirst(...); // code-first

// alternative for contract-first if not also using protobuf-net.Grpc, which provides the Bind API
// YourService.BindService(server.ServiceBinder.Intercept(...), new MyService());
```

## Other notes

It currently targets .NET Framework 4.7.2 up to .NET 6.0, using newer features when available. It is still very experimental - but most core things should work; feedback is welcome.

Known gaps:

- gRPC auth (although transport auth works fine)
- per-stream service activation (rather than singleton)
- testing needs more coverage
- per-stream backoff negotiation; designed, not yet implemented
- for some reason the server implementation isn't working 100% with SAEA currently - hence `.AsStream().AsFrames()` instead of just `.AsFrames()`
- open question around interceptor order