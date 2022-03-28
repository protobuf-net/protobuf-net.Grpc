using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class NullConnection : IFrameConnection
{
    private readonly ChannelReader<(Frame Frame, FrameWriteFlags Flags)> _input;
    private readonly ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> _output;

    public ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> Output => _output;

    internal static void CreateLinkedPair(out IFrameConnection x, out IFrameConnection y)
    {
        var a = Channel.CreateUnbounded<(Frame Frame, FrameWriteFlags Flags)>(Utilities.UnboundedChannelOptions_SingleReadMultiWriterNoSync);
        var b = Channel.CreateUnbounded<(Frame Frame, FrameWriteFlags Flags)>(Utilities.UnboundedChannelOptions_SingleReadMultiWriterNoSync);

        x = new NullConnection(a.Reader, b.Writer);
        y = new NullConnection(b.Reader, a.Writer);
    }
    public NullConnection(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> input, ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> output)
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
        => _input.GetAsyncEnumerator(_output, static pair => pair.Frame, cancellationToken);

    Task IFrameConnection.WriteAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken)
        => source.Completion;
}
