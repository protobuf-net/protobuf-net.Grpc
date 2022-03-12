namespace ProtoBuf.Grpc.Lite.Internal;

internal interface IGatedTerminator : ITerminator { }
internal interface ITerminator : IAsyncEnumerable<Frame>, IAsyncDisposable
{
    ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken);
}
