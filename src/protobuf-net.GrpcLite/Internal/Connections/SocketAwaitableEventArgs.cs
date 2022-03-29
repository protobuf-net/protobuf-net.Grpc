using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

// borrowed (with love) from Pipelines.Sockets.Unofficial

/// <summary>
/// Awaitable SocketAsyncEventArgs, where awaiting the args yields either the BytesTransferred or throws the relevant socket exception
/// </summary>
internal sealed class SocketAwaitableEventArgs : SocketAsyncEventArgs, ICriticalNotifyCompletion
{
    /// <summary>
    /// Abort the current async operation (and prevent future operations)
    /// </summary>
    public void Abort(SocketError error = SocketError.OperationAborted)
    {
        if (error == SocketError.Success) Throw();
        try
        {
            _forcedError = error;
            OnCompleted(this);
        }
        catch (Exception ex)
        {
            Logging.DebugWriteLine(ex.Message);
        }
        static void Throw() => throw new ArgumentOutOfRangeException(nameof(error));
    }

    private volatile SocketError _forcedError; // Success = 0, no field init required

    private static readonly Action _callbackCompleted = () => { };

    private Action? _callback;

    internal static readonly Action<object> InvokeStateAsAction = state => ((Action)state)();


    /// <summary>
    /// Get the awaiter for this instance; used as part of "await"
    /// </summary>
    public SocketAwaitableEventArgs GetAwaiter() => this;

    /// <summary>
    /// Indicates whether the current operation is complete; used as part of "await"
    /// </summary>
    public bool IsCompleted => ReferenceEquals(_callback, _callbackCompleted);

    /// <summary>
    /// Gets the result of the async operation is complete; used as part of "await"
    /// </summary>
    public int GetResult()
    {
        Debug.Assert(ReferenceEquals(_callback, _callbackCompleted));

        _callback = null;

        if (_forcedError != SocketError.Success)
        {
            ThrowSocketException(_forcedError);
        }

        if (SocketError != SocketError.Success)
        {
            ThrowSocketException(SocketError);
        }

        return BytesTransferred;

        static void ThrowSocketException(SocketError e)
            => throw new SocketException((int)e);
    }

    /// <summary>
    /// Schedules a continuation for this operation; used as part of "await"
    /// </summary>
    public void OnCompleted(Action continuation)
    {
        if (ReferenceEquals(Volatile.Read(ref _callback), _callbackCompleted)
            || ReferenceEquals(Interlocked.CompareExchange(ref _callback, continuation, null), _callbackCompleted))
        {
            continuation();
        }
    }

    /// <summary>
    /// Schedules a continuation for this operation; used as part of "await"
    /// </summary>
    public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);

    /// <summary>
    /// Marks the operation as complete - this should be invoked whenever a SocketAsyncEventArgs operation returns false
    /// </summary>
    public void Complete() => OnCompleted(this);

    /// <summary>
    /// Invoked automatically when an operation completes asynchronously
    /// </summary>
    protected override void OnCompleted(SocketAsyncEventArgs e)
    {
        var continuation = Interlocked.Exchange(ref _callback, _callbackCompleted);
        continuation?.Invoke();
    }
}
