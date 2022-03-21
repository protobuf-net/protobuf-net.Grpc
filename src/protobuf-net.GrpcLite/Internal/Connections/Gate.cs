using ProtoBuf.Grpc.Lite.Connections;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal abstract class Gate : IFrameConnection
{
    bool IFrameConnection.ThreadSafeWrite => true;
    protected IFrameConnection Tail { get; }
    private Channel<Frame>? _outputBuffer;
    protected Gate(IFrameConnection tail, int outputBuffer)
    {
        Tail = tail;
        if (outputBuffer > 0)
        {
            _outputBuffer = outputBuffer == int.MaxValue
                ? Channel.CreateUnbounded<Frame>(_unbounded ??= new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false,
                })
                : Channel.CreateBounded<Frame>(new BoundedChannelOptions(outputBuffer)
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = BoundedChannelFullMode.Wait,
                });
        }
    }

    public virtual Task Complete => _outputBuffer?.Reader.Completion ?? Tail.Complete;

    public virtual void Close(Exception? exception)
    {
        Tail?.Close(exception);
        _outputBuffer?.Writer.Complete(exception);
    }

    private static UnboundedChannelOptions? _unbounded;

    public virtual ValueTask DisposeAsync() => Tail.DisposeAsync();
    public IAsyncEnumerator<Frame> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        var inner = Tail.GetAsyncEnumerator(cancellationToken);
        if (_outputBuffer is null) return inner;

        _ = BufferAsync(inner, _outputBuffer.Writer, cancellationToken);
        return _outputBuffer.Reader.GetAsyncEnumerator(_outputBuffer.Writer, cancellationToken);
    }

    private static async Task BufferAsync(IAsyncEnumerator<Frame> source, ChannelWriter<Frame> destination, CancellationToken cancellationToken)
    {
        try
        {
            while (await source.MoveNextAsync())
            {
                var value = source.Current;
                if (!destination.TryWrite(value))
                {
                    await destination.WriteAsync(value, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            destination.TryComplete(ex);
        }
        finally
        {
            await source.SafeDisposeAsync();
        }
    }

    public abstract ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken);

    public virtual ValueTask WriteAsync(ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken = default)
        => this.WriteAllAsync(frames, cancellationToken);

    public abstract ValueTask FlushAsync(CancellationToken cancellationToken);
}
