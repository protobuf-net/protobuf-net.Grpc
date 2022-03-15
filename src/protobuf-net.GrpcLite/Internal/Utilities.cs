using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static class Utilities
{
    public static readonly byte[] EmptyBuffer = Array.Empty<byte>(); // static readonly field to make the JIT's life easy

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogDebug<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Debug, default, state, exception, formatter);

    public static void SafeDispose(this IDisposable? disposable)
    {
        if (disposable is not null)
        {
            try { disposable.Dispose(); }
            catch { }
        }
    }
    public static ValueTask SafeDisposeAsync(this IAsyncDisposable? disposable)
    {
        if (disposable is not null)
        {
            try
            {
                var pending = disposable.DisposeAsync();
                if (!pending.IsCompleted) return CatchAsync(pending);
                // we always need to observe it, for both success and failure
                pending.GetAwaiter().GetResult();
            }
            catch { } // swallow
        }
        return default;

        static async ValueTask CatchAsync(ValueTask pending)
        {
            try { await pending; }
            catch { } // swallow
        }
    }

    public static ValueTask SafeDisposeAsync(IAsyncDisposable? first, IAsyncDisposable? second)
    {
        // handle null/same
        if (first is null || ReferenceEquals(first, second)) return second.SafeDisposeAsync();
        if (second is null) return first.SafeDisposeAsync();

        // so: different
        var firstPending = first.SafeDisposeAsync();
        var secondPending = second.SafeDisposeAsync();
        if (firstPending.IsCompletedSuccessfully)
        {
            firstPending.GetAwaiter().GetResult(); // ensuure observed
            return secondPending;
        }
        if (secondPending.IsCompletedSuccessfully)
        {
            secondPending.GetAwaiter().GetResult();
            return firstPending;
        }
        // so: neither completed synchronously!
        return Both(firstPending, secondPending);
        static async ValueTask Both(ValueTask first, ValueTask second)
        {
            await first;
            await second;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogInformation<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Information, default, state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogInformation(this ILogger? logger, string message)
        => logger?.Log<string>(LogLevel.Information, default, message, null, static (state, _) => state);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Error, default, state, exception, formatter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogError(this ILogger? logger, Exception exception, [CallerMemberName] string caller = "")
    {
        if (logger is not null) LogSafe(logger, LogLevel.Error, exception, caller);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogCritical(this ILogger? logger, Exception exception, [CallerMemberName] string caller = "")
    {
        if (logger is not null) LogSafe(logger, LogLevel.Critical, exception, caller);
    }

    static void LogSafe(ILogger logger, LogLevel level, Exception exception, string caller)
    {
        try // we're probably already in a catch block; don't make things worse!
        {
            logger.Log<string>(level, default, caller, exception, static (s, ex) => $"[{s}]: {ex!.Message}");
        }
        catch { }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogCritical<TState>(this ILogger? logger, TState state, Func<TState, Exception?, string> formatter, Exception? exception = null)
        => logger?.Log<TState>(LogLevel.Critical, default, state, exception, formatter);

    public static readonly Task<bool> AsyncTrue = Task.FromResult(true), AsyncFalse = Task.FromResult(false);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort IncrementToUInt32(ref int value)
        => unchecked((ushort)Interlocked.Increment(ref value));

    private static readonly ArraySegment<byte> EmptySegment = new ArraySegment<byte>(EmptyBuffer);
    internal static bool TryGetEmptySegment(out ArraySegment<byte> segment)
    {
        segment = EmptySegment;
        return true;
    }

    public static ValueTask WriteAllAsync(this IFrameConnection connection, ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken = default)
    {
        return frames.Length switch
        {
            0 => default,
            1 => connection.WriteAsync(frames.Span[0], cancellationToken),
            _ => SlowAsync(connection, frames, cancellationToken),
        };
        async static ValueTask SlowAsync(IFrameConnection connection, ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken)
        {
            var length = frames.Length;
            for (int i = 0; i < length; i++)
            {
                await connection.WriteAsync(frames.Span[i], cancellationToken);
            }
        }
    }

    internal static Task GetLazyCompletion(ref object? taskOrCompletion, bool markComplete)
    {   // lazily process _completion
        while (true)
        {
            switch (Volatile.Read(ref taskOrCompletion))
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
                    if (Interlocked.CompareExchange(ref taskOrCompletion, newFieldValue, null) is null)
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

    internal static ValueTask AsValueTask(this Exception ex)
    {
#if NET5_0_OR_GREATER
        return ValueTask.FromException(ex);
#else
        return new ValueTask(Task.FromException(ex));
#endif
    }

#if NETCOREAPP3_1_OR_GREATER
    public static void StartWorker(this IWorker worker)
        => ThreadPool.UnsafeQueueUserWorkItem(worker, preferLocal: false);
#else
    public static void StartWorker(this IWorker worker)
        => ThreadPool.UnsafeQueueUserWorkItem(s_StartWorker, worker);
    private static readonly WaitCallback s_StartWorker = state => (Unsafe.As<IWorker>(state)).Execute();
#endif
}
#if NETCOREAPP3_1_OR_GREATER
internal interface IWorker : IThreadPoolWorkItem {}
#else
internal interface IWorker
{
    void Execute();
}
#endif
