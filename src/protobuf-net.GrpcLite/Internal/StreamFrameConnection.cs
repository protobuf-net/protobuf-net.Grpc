using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class StreamFrameConnection : IFrameConnection, IValueTaskSource
{
    bool IFrameConnection.ThreadSafeWrite => false;
    private readonly Stream _input, _output;

    private readonly Action _writeComplete;
    private ValueTaskAwaiter _pendingWrite;
    private ManualResetValueTaskSourceCore<bool> _vts;
    private object? _completion;




    NewFrame _writingFrame;

    public StreamFrameConnection(Stream input, Stream output)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!input.CanRead) throw new ArgumentException("Cannot read from input stream", nameof(input));
        if (!output.CanWrite) throw new ArgumentException("Cannot write to output stream", nameof(output));
        _input = input;
        _output = output;
        _writeComplete = () =>
        {
            var frame = _writingFrame;
            var pending = _pendingWrite;
            frame = default;
            _pendingWrite = default;
            try
            {
                frame.Release();
                pending.GetResult();
                _vts.SetResult(true);
            }
            catch (Exception ex)
            {
                _vts.SetException(ex);
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        await Utilities.SafeDisposeAsync(_input, _output);
        _ = GetCompletion(true);
    }

    Task IFrameConnection.Complete => GetCompletion(false);


    private Task GetCompletion(bool markComplete)
    {   // lazily process _completion
        while (true)
        {
            switch (Volatile.Read(ref _completion))
            {
                case null:
                    // try to swap in Task.CompletedTask
                    object newFieldValue;
                    Task result;
                    if (markComplete)
                    {
                        newFieldValue = result = Task.CompletedTask;
                    }
                    else
                    {
                        var newTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        newFieldValue = newTcs;
                        result = newTcs.Task;
                    }
                    if (Interlocked.CompareExchange(ref _completion, newFieldValue, null) is null)
                    {
                        return result;
                    }
                    continue; // if we fail the swap: redo from start
                case Task task:
                    return task; // this will be Task.CompletedTask
                case TaskCompletionSource<bool> tcs:
                    if (markComplete) tcs.TrySetResult(true);
                    return tcs.Task;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
    public async IAsyncEnumerator<NewFrame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        byte[] headerBuffer = new byte[FrameHeader.Size];
        while (!cancellationToken.IsCancellationRequested)
        {
            int remaining = FrameHeader.Size, offset = 0, bytesRead;
            while (remaining > 0 && (bytesRead = await _input.ReadAsync(headerBuffer, offset, remaining, cancellationToken)) > 0)
            {
                remaining -= bytesRead;
                offset += bytesRead;
            }
            if (remaining == FrameHeader.Size) yield break; // clean EOF
            if (remaining != 0) ThrowEOF();

            var header = FrameHeader.ReadUnsafe(in headerBuffer[0]);

            // note we rent a new buffer even for zero-length payloads, so we can return a frame based on that segment
            NewFrame.AssertValidLength(header.PayloadLength);
            var slab = FrameBufferManager.Shared.Rent(header.PayloadLength);
            NewFrame frame;
            try
            {
                if (header.PayloadLength != 0)
                {
                    var payload = slab.ActiveBuffer.Slice(0, header.PayloadLength);
                    while (!payload.IsEmpty && (bytesRead = await _input.ReadAsync(payload, cancellationToken)) > 0)
                    {
                        payload = payload.Slice(bytesRead);
                    }
                    if (!payload.IsEmpty) ThrowEOF();
                    slab.Advance(header.PayloadLength);
                }
                frame = slab.CreateFrameAndInvalidate(header, updateHeaderLength: false);
            }
            finally
            {
                slab?.Return();
            }
            yield return frame;

        }
        static void ThrowEOF() => throw new EndOfStreamException();
    }

    public ValueTask WriteAsync(NewFrame frame, CancellationToken cancellationToken)
    {
        var pending = _output.WriteAsync(frame.Buffer, cancellationToken);
        if (pending.IsCompleted)
        {
            frame.Release();
            return pending;
        }
        return ScheduleRelease(frame, pending);
    }
    private ValueTask ScheduleRelease(in NewFrame frame, in ValueTask pending)
    {
        _pendingWrite = pending.GetAwaiter();
        _writingFrame = frame;
        _pendingWrite.UnsafeOnCompleted(_writeComplete);
        return new ValueTask(this, _vts.Version);
    }

    void IValueTaskSource.GetResult(short token)
    {
        try
        {
            _vts.GetResult(token);
        }
        finally
        {
            _vts.Reset();
        }
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => _vts.GetStatus(token);

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _vts.OnCompleted(continuation, state, token, flags);

    public ValueTask WriteAsync(ReadOnlyMemory<NewFrame> frames, CancellationToken cancellationToken = default)
        => this.WriteAllAsync(frames, cancellationToken);

    void IFrameConnection.Close(Exception? exception)
    {
        _ = DisposeAsync().AsTask();
    }
}
