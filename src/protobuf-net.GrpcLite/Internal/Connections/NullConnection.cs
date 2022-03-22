using ProtoBuf.Grpc.Lite.Connections;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class NullConnection : IFrameConnection
{
    private readonly ChannelReader<Frame> _input;
    private readonly ChannelWriter<Frame> _output;

    public ChannelWriter<Frame> Output => _output;

    internal static void CreateLinkedPair(out IFrameConnection x, out IFrameConnection y)
    {
        var a = Channel.CreateUnbounded<Frame>(Utilities.UnboundedChannelOptions_SingleReadMultiWriterNoSync);
        var b = Channel.CreateUnbounded<Frame>(Utilities.UnboundedChannelOptions_SingleReadMultiWriterNoSync);

        x = new NullConnection(a.Reader, b.Writer);
        y = new NullConnection(b.Reader, a.Writer);
    }
    public NullConnection(ChannelReader<Frame> input, ChannelWriter<Frame> output)
    {
        _input = input;
        _output = output;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _output.TryComplete();
        return default;
    }

    IAsyncEnumerator<Frame> IAsyncEnumerable<Frame>.GetAsyncEnumerator(CancellationToken cancellationToken)
        => _input.GetAsyncEnumerator(_output, cancellationToken);

    Task IFrameConnection.WriteAsync(ChannelReader<Frame> source, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
