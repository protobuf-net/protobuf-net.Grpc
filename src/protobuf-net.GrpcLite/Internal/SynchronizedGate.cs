using ProtoBuf.Grpc.Lite.Connections;

namespace ProtoBuf.Grpc.Lite.Internal;


internal sealed class SynchronizedGate : Gate
{
    public SynchronizedGate(IFrameConnection tail, int outputBuffer) : base(tail, outputBuffer) { }

    private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);

    public override ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        bool release = false;
        try
        {
            release = _mutex.Wait(0);
            if (release)
            {
                var pending = Tail.FlushAsync(cancellationToken);
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
                return FullAsync(this, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return ex.AsValueTask();
        }
        finally
        {
            if (release) _mutex.Release();
        }
        static async ValueTask FullAsync(SynchronizedGate gate, CancellationToken cancellationToken)
        {
            await gate._mutex.WaitAsync(cancellationToken);
            try
            {
                await gate.Tail.FlushAsync(cancellationToken);
            }
            finally
            {
                gate._mutex.Release();
            }
        }
    }
    static async ValueTask AwaitAndRelease(ValueTask pending, SemaphoreSlim mutex)
    {
        try { await pending; }
        finally { mutex.Release(); }
    }
    public override ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken)
    {
        bool release = false;
        try
        {
            release = _mutex.Wait(0);
            if (release)
            {
                var pending = Tail.WriteAsync(frame, cancellationToken);
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
                return FullAsync(this, frame, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return ex.AsValueTask();
        }
        finally
        {
            if (release) _mutex.Release();
        }
        static async ValueTask FullAsync(SynchronizedGate gate, Frame frame, CancellationToken cancellationToken)
        {
            await gate._mutex.WaitAsync(cancellationToken);
            try
            {
                await gate.Tail.WriteAsync(frame, cancellationToken);
            }
            finally
            {
                gate._mutex.Release();
            }
        }
    }
    public override ValueTask DisposeAsync()
    {
        _mutex.SafeDispose();
        return default;
    }
}