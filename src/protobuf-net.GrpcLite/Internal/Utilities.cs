using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal;

internal static partial class Utilities
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
    public static bool IsAlive<T>(this Memory<T> memory)
    {
        return MemoryMarshal.TryGetMemoryManager<T, RefCountedMemoryManager<T>>(memory, out var manager)
            ? manager.IsAlive : true; // if it isn't ref-counted, it is always "alive"
    }
    public static bool IsAlive<T>(this ReadOnlyMemory<T> memory)
    {
        return MemoryMarshal.TryGetMemoryManager<T, RefCountedMemoryManager<T>>(memory, out var manager)
            ? manager.IsAlive : true; // if it isn't ref-counted, it is always "alive"
    }

    public static long PayloadLength(this ReadOnlySpan<Frame> frames)
    {
        long length = 0;
        foreach (var frame in frames)
        {
            if (frame.HasValue)
            {
                length += frame.TotalLength - FrameHeader.Size;
            }
        }
        return length;
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

    public static Task StartWriterAsync(this IFrameConnection connection, IConnection owner, out ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> writer, CancellationToken cancellationToken)
    {
        if (connection is NullConnection nil)
        {
            writer = nil.Output; // use the pre-existing output directly
            return Task.CompletedTask;
        }

        var channel = Channel.CreateUnbounded<(Frame Frame, FrameWriteFlags Flags)>(UnboundedChannelOptions_SingleReadMultiWriterNoSync);
        writer = channel.Writer;
        return WithCapture(connection, owner, channel.Reader, cancellationToken);

        static Task WithCapture(IFrameConnection connection, IConnection owner, ChannelReader<(Frame Frame, FrameWriteFlags Flags)> reader, CancellationToken cancellationToken)
            => Task.Run(async () =>
            {
                try
                {
                    Logging.SetSource(null, owner.IsClient ? LogKind.Client : LogKind.Server, "writer");
                    await connection.WriteAsync(reader, cancellationToken);
                    owner.Close(null);
                }
                catch (Exception ex)
                {
                    owner.Close(ex);
                }
            });
    }

    public static string CreateString(this ArraySegment<char> value)
        => value.Count == 0 ? "" : new string(value.Array ?? Array.Empty<char>(), value.Offset, value.Count);

#if NETSTANDARD2_1 || NET472
    // note: here we use the sofware fallback implementation from the BCL
    // source: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Numerics/BitOperations.cs
    // "The .NET Foundation licenses this file to you under the MIT license." (so: we're fine for licensing)
    // With full credit to the donet runtime

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LeadingZeroCount(uint value)
        => 31 ^ Log2SoftwareFallback(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LeadingZeroCount(int value)
        => 31 ^ Log2SoftwareFallback(unchecked((uint)value));

    private static int Log2SoftwareFallback(uint value)
    {
        // No AggressiveInlining due to large method size
        // Has conventional contract 0->0 (Log(0) is undefined)

        // Fill trailing zeros with ones, eg 00010010 becomes 00011111
        value |= value >> 01;
        value |= value >> 02;
        value |= value >> 04;
        value |= value >> 08;
        value |= value >> 16;

        // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
        return Unsafe.AddByteOffset(
            // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_1100_0100_1010_1100_1101_1101u
            ref MemoryMarshal.GetReference(Log2DeBruijn),
            // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
            (IntPtr)(int)((value * 0x07C4ACDDu) >> 27));
    }
    private static ReadOnlySpan<byte> Log2DeBruijn => new byte[32]
    {
        00, 09, 01, 10, 13, 21, 02, 29,
        11, 14, 16, 18, 22, 25, 03, 30,
        08, 12, 20, 28, 15, 17, 24, 07,
        19, 27, 23, 06, 26, 05, 04, 31
    };
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LeadingZeroCount(uint value)
        => System.Numerics.BitOperations.LeadingZeroCount(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LeadingZeroCount(int value)
        => System.Numerics.BitOperations.LeadingZeroCount(unchecked((uint)value));
#endif

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

    public static IAsyncEnumerator<TValue> GetAsyncEnumerator<T, TValue>(this ChannelReader<T> input, ChannelWriter<T>? closeOutput,
        Func<T, TValue> selector, CancellationToken cancellationToken)
    {
        return closeOutput is not null ? FullyChecked(input, closeOutput, selector, cancellationToken)
            : Simple(input, selector, cancellationToken);

        static async IAsyncEnumerator<TValue> Simple(ChannelReader<T> input, Func<T, TValue> selector, CancellationToken cancellationToken)
        {
            do
            {
                while (input.TryRead(out var item))
                    yield return selector(item);
            }
            while (await input.WaitToReadAsync(cancellationToken));
        }

        static async IAsyncEnumerator<TValue> FullyChecked(ChannelReader<T> input, ChannelWriter<T>? closeOutput, Func<T, TValue> selector, CancellationToken cancellationToken)
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
                    if (haveItem) yield return selector(item!);
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
