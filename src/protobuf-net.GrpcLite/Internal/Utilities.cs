using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static class Utilities
{
    public static readonly byte[] EmptyBuffer = Array.Empty<byte>(); // static readonly field to make the JIT's life easy

    public static void SafeDispose<T>(this T disposable) where T : struct, IDisposable
    {
        try { disposable.Dispose(); }
        catch { }
    }
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

    public static string ToHex(this Memory<byte> data) => ToHex((ReadOnlyMemory<byte>)data);
    public static string ToHex(this ReadOnlyMemory<byte> data)
    {
#if NET5_0_OR_GREATER
        return Convert.ToHexString(data.Span);
#else
        if (MemoryMarshal.TryGetArray<byte>(data, out var segment) && segment.Array is not null)
        {
            return BitConverter.ToString(segment.Array, segment.Offset, segment.Count);
        }
        return "n/a";
#endif
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


    public static readonly Task<bool> AsyncTrue = Task.FromResult(true), AsyncFalse = Task.FromResult(false);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort IncrementToUInt32(ref int value)
        => unchecked((ushort)Interlocked.Increment(ref value));

    public static Stream CheckDuplex(this Stream duplex)
    {
        if (duplex is null) throw new ArgumentNullException(nameof(duplex));
        if (!duplex.CanRead) throw new ArgumentException("Cannot read from stream", nameof(duplex));
        if (!duplex.CanWrite) throw new ArgumentException("Cannot write to stream", nameof(duplex));
        if (duplex.CanSeek) throw new ArgumentException("Stream is seekable, so cannot be duplex", nameof(duplex));
        return duplex;
    }

    public static Task StartWriterAsync(this IFrameConnection connection, bool isClient, out ChannelWriter<Frame> writer, CancellationToken cancellationToken)
    {
        if (connection is NullConnection nil)
        {
            writer = nil.Output; // use the pre-existing output directly
            return Task.CompletedTask;
        }

        var channel = Channel.CreateUnbounded<Frame>(UnboundedChannelOptions_SingleReadMultiWriterNoSync);
        writer = channel.Writer;
        return WithCapture(connection, isClient, channel.Reader, cancellationToken);

        static Task WithCapture(IFrameConnection connection, bool isClient, ChannelReader<Frame> reader, CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                Logging.SetSource(null, isClient ? LogKind.Client : LogKind.Server, "writer");
                return connection.WriteAsync(reader, cancellationToken);
            });
    }

    public static readonly UnboundedChannelOptions UnboundedChannelOptions_SingleReadMultiWriterNoSync = new UnboundedChannelOptions
    {
        AllowSynchronousContinuations = false,
        SingleReader = true,
        SingleWriter = false,
    };

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

    public static Task IncompleteTask { get; } = AsyncTaskMethodBuilder.Create().Task;

    public static IAsyncEnumerator<T> GetAsyncEnumerator<T>(this ChannelReader<T> input, ChannelWriter<T>? closeOutput = null, CancellationToken cancellationToken = default)
    {
        return closeOutput is not null ? FullyChecked(input, closeOutput, cancellationToken)
            : Simple(input, cancellationToken);

        static async IAsyncEnumerator<T> Simple(ChannelReader<T> input, CancellationToken cancellationToken)
        {
            do
            {
                while (input.TryRead(out var item))
                    yield return item;
            }
            while (await input.WaitToReadAsync(cancellationToken));
        }

        static async IAsyncEnumerator<T> FullyChecked(ChannelReader<T> input, ChannelWriter<T>? closeOutput, CancellationToken cancellationToken)
        {
            // we need to do some code gymnastics to ensure that we close the connection (with an exception
            // as necessary) in all cases
            while (true)
            {
                bool haveItem;
                T? item;
                do
                {
                    try
                    {
                        haveItem = input.TryRead(out item);
                    }
                    catch (Exception ex)
                    {
                        closeOutput?.TryComplete(ex);
                        throw;
                    }
                    if (haveItem) yield return item!;
                }
                while (haveItem);

                try
                {
                    if (!await input.WaitToReadAsync(cancellationToken))
                    {
                        closeOutput?.TryComplete();
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    closeOutput?.TryComplete(ex);
                    throw;
                }
            }
        }
    }

    internal static CancellationTokenRegistration RegisterCancellation(this IStream stream, CancellationToken cancellationToken)
    {
        if (stream is null || !cancellationToken.CanBeCanceled || cancellationToken == stream.CancellationToken
            || cancellationToken == stream.Connection?.Shutdown)
        {
            return default; // nothing to do, or we'd already be handling it because it is our own CT
        }
        cancellationToken.ThrowIfCancellationRequested();
        return cancellationToken.Register(s_CancelStream, stream, false);
    }
    private static readonly Action<object?> s_CancelStream = static state => Unsafe.As<IStream>(state!).Cancel();

}
#if NETCOREAPP3_1_OR_GREATER
internal interface IWorker : IThreadPoolWorkItem {}
#else
internal interface IWorker
{
    void Execute();
}
#endif
