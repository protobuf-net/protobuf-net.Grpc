using ProtoBuf.Grpc.Lite.Connections;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

internal abstract class Gate : ITerminator
{
    protected ITerminator Tail { get; }
    private Channel<NewFrame>? _outputBuffer;
    protected Gate(ITerminator terminator, int outputBuffer)
    {
        Tail = terminator;
        if (outputBuffer > 0)
        {
            _outputBuffer = outputBuffer == int.MaxValue
                ? Channel.CreateUnbounded<NewFrame>(_unbounded ??= new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false,
                })
                : Channel.CreateBounded<NewFrame>(new BoundedChannelOptions(outputBuffer)
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = BoundedChannelFullMode.Wait,
                });
        }
    }

    private static UnboundedChannelOptions? _unbounded;

    public virtual ValueTask DisposeAsync() => Tail.DisposeAsync();
    public IAsyncEnumerator<NewFrame> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        var inner = Tail.GetAsyncEnumerator(cancellationToken);
        if (_outputBuffer is null) return inner;
        _ = BufferAsync(inner, _outputBuffer.Writer, cancellationToken);
        return ReadAllAsync(_outputBuffer, cancellationToken);
    }

    private static async IAsyncEnumerator<NewFrame> ReadAllAsync(Channel<NewFrame> channel, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                if (!await channel.Reader.WaitToReadAsync(cancellationToken))
                    yield break; // all done
            }
            catch (Exception ex)
            {   // fail
                channel.Writer.TryComplete(ex);
            }
            while (channel.Reader.TryRead(out var value)) yield return value;
        }
    }

    private static async Task BufferAsync(IAsyncEnumerator<NewFrame> source, ChannelWriter<NewFrame> destination, CancellationToken cancellationToken)
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

    public abstract ValueTask WriteAsync(BufferSegment frames, CancellationToken cancellationToken);
}

internal sealed class SynchronizedGate : Gate
{
    public SynchronizedGate(ITerminator terminator, int outputBuffer) : base(terminator, outputBuffer) { }

    private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);
    public override ValueTask WriteAsync(BufferSegment frames, CancellationToken cancellationToken)
    {
        bool release = false;
        try
        {
            release = _mutex.Wait(0);
            if (release)
            {
                var pending = Tail.WriteAsync(frames, cancellationToken);
                if (pending.IsCompleted)
                {
                    pending.GetAwaiter().GetResult();
                    return default;
                }
                else
                {
                    release = false;
                    return AwaitAndRelease(pending, _mutex);
                }
            }
            else
            {
                return FullAsync(this, frames, cancellationToken);
            }
        }
        catch (Exception ex)
        {
#if NET5_0_OR_GREATER
            return ValueTask.FromException(ex);
#else
            return new ValueTask(Task.FromException(ex));
#endif
        }
        finally
        {
            if (release) _mutex.Release();
        }
        static async ValueTask FullAsync(SynchronizedGate gate, BufferSegment frames, CancellationToken cancellationToken)
        {
            await gate._mutex.WaitAsync(cancellationToken);
            try
            {
                await gate.Tail.WriteAsync(frames, cancellationToken);
            }
            finally
            {
                gate._mutex.Release();
            }
        }
        static async ValueTask AwaitAndRelease(ValueTask pending, SemaphoreSlim mutex)
        {
            try
            {
                await pending;
            }
            finally
            {
                mutex.Release();
            }
        }
    }
    public override ValueTask DisposeAsync()
    {
        _mutex.SafeDispose();
        return default;
    }
}

internal sealed class BufferedGate : Gate
{
    readonly Channel<BufferSegment> _inputBuffer;
    public BufferedGate(ITerminator terminator, int inputBuffer, int outputBuffer) : base(terminator, outputBuffer)
    {
        _inputBuffer = inputBuffer == int.MaxValue
            ? Channel.CreateUnbounded<BufferSegment>(_unboundedOptions ??= new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true,
                AllowSynchronousContinuations = false,
            })
            : Channel.CreateBounded<BufferSegment>(new BoundedChannelOptions(inputBuffer)
            {
                SingleWriter = false,
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });
    }
    public override ValueTask WriteAsync(BufferSegment frames, CancellationToken cancellationToken)
        => _inputBuffer.Writer.WriteAsync(frames, cancellationToken);

    static UnboundedChannelOptions? _unboundedOptions;
    public override ValueTask DisposeAsync()
    {
        _inputBuffer.Writer.TryComplete();
        return default;
    }

}