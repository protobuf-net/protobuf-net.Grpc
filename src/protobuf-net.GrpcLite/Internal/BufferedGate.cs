using ProtoBuf.Grpc.Lite.Connections;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;


internal sealed class BufferedGate : Gate
{
    readonly Channel<Frame> _inputBuffer;
    public BufferedGate(IFrameConnection tail, int inputBuffer, int outputBuffer) : base(tail, outputBuffer)
    {
        _inputBuffer = inputBuffer == int.MaxValue
            ? Channel.CreateUnbounded<Frame>(_unboundedOptions ??= new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true,
                AllowSynchronousContinuations = false,
            })
            : Channel.CreateBounded<Frame>(new BoundedChannelOptions(inputBuffer)
            {
                SingleWriter = false,
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });
    }
    public override ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
        => _inputBuffer.Writer.WriteAsync(frame, cancellationToken);

    static UnboundedChannelOptions? _unboundedOptions;

    public override ValueTask FlushAsync(CancellationToken cancellationToken)
        => default; // nothing to do

    public override ValueTask DisposeAsync()
    {
        _inputBuffer.Writer.TryComplete();
        return default;
    }
}