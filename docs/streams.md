# Streams

gRPC has a `stream` concept that allows client-streaming, server-streaming, and full-duplex (independent bidirectional) streaming of messages. Inside
protobuf-net.Grpc, this is typically exposed via the `IAsyncEnumerable<T>` API, which is an asynchronous sequence of messages of type `T`. For example,

```
public async IAsyncEnumerable<SomeResponse> SomeDuplexMethod(IAsyncEnumerable<SomeRequest> requests)
{
    // very basic request/response server using streaming
    await foreach (var req in requests)
    {
        yield return ApplySomeTransformation(req);
    }
}
```

This is *fine*, but .NET has another "stream", i.e. `System.IO.Stream` - a sequence of *bytes*.

As of 1.2.2, protobuf-net.Grpc has limited (and growing) support for `Stream` as an exchange mechanism. Currently supported scenarios:

- `Task<Stream> SomeMethod(/* optional single request message, optional context/cancellation */);`
- `ValueTask<Stream> SomeMethod(/* optional single request message, optional context/cancellation */);`

For example:

``` c#
public async Task<Stream> GetFileContents(SomeRequest request)
{
    var localPath = await CheckAccessAndMapToLocalPath(request.Path);

    return File.OpenRead(localPath);
}
```

This hands a `Stream` back to the library, with the library assuming control of how to transmit that, disposing the stream when done (as an implementation detail: it
is sent as a `stream` of [`BytesValue`](https://github.com/protocolbuffers/protobuf/blob/main/src/google/protobuf/wrappers.proto) messages,
with bespoke marshalling). As you would expect, the client can access this data trivially:

``` c#
await using var data = proxy.GetFileContents(request);
await using var localFile = File.Create(localCachePath);
await data.CopyToAsync(localFile);
```

---

These are just trivial examples; more complex scenarios are possible, for example using `Pipe` on the server to allow the worker to provide
data after the initial response (this will be more direct when the supported APIs are extended to include pipes directly).