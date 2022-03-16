using ProtoBuf.Grpc.Lite.Connections;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class NullConnection : IFrameConnection
{
    static readonly UnboundedChannelOptions s_Options = new UnboundedChannelOptions
    {
        AllowSynchronousContinuations = false,
        SingleReader = true,
        SingleWriter = false,
    };
    private readonly ChannelReader<Frame> _input;
    private readonly ChannelWriter<Frame> _output;

    internal static void CreateLinkedPair(out IFrameConnection x, out IFrameConnection y)
    {
        var a = Channel.CreateUnbounded<Frame>(s_Options);
        var b = Channel.CreateUnbounded<Frame>(s_Options);

        x = new NullConnection(a.Reader, b.Writer);
        y = new NullConnection(b.Reader, a.Writer);
    }
    public NullConnection(ChannelReader<Frame> input, ChannelWriter<Frame> output)
    {
        _input = input;
        _output = output;
    }

    bool IFrameConnection.ThreadSafeWrite => true;

    Task IFrameConnection.Complete => _input.Completion;

    void IFrameConnection.Close(Exception? exception)
        => _output.TryComplete(exception);

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _output.TryComplete();
        return default;
    }

    ValueTask IFrameConnection.FlushAsync(CancellationToken cancellationToken)
        => default;

    IAsyncEnumerator<Frame> IAsyncEnumerable<Frame>.GetAsyncEnumerator(CancellationToken cancellationToken)
        => _input.GetAsyncEnumerator(_output, cancellationToken);

    ValueTask IFrameConnection.WriteAsync(Frame frame, CancellationToken cancellationToken)
        => _output.WriteAsync(frame, cancellationToken);

    ValueTask IFrameConnection.WriteAsync(ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken)
        => this.WriteAllAsync(frames, cancellationToken);
}
