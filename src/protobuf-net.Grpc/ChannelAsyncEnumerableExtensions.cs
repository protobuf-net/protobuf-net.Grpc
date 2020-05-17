using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Provides utility methods for working with asynchronous sequence and channels
    /// </summary>
    public static class ChannelAsyncEnumerableExtensions
    {
#if PLAT_NO_CHANNEL_READALLASYNC
        /// <summary>
        /// Consumes a channel as an asynchronous sequence
        /// </summary>
        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this Channel<T> channel, CancellationToken cancellationToken = default)
            => AsAsyncEnumerable(channel.Reader, cancellationToken);

        /// <summary>
        /// Consumes a channel as an asynchronous sequence
        /// </summary>
        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this Channel<T> channel, bool closeWriterOnCancellation, CancellationToken cancellationToken = default)
            // note: we can't check CanBeCancelled too early here, as the CT passed to the iterator can be *different*
            => closeWriterOnCancellation ? AsAsyncEnumerableCloseWriter(channel, cancellationToken) : AsAsyncEnumerable(channel.Reader, cancellationToken);

        /// <summary>
        /// Consumes a channel as an asynchronous sequence
        /// </summary>
        public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this ChannelReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out T item))
                {
                    yield return item;
                }
            }
        }
#else
        /// <summary>
        /// Consumes a channel as an asynchronous sequence
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this Channel<T> channel, CancellationToken cancellationToken = default)
            => channel.Reader.ReadAllAsync(cancellationToken);

        /// <summary>
        /// Consumes a channel as an asynchronous sequence
        /// </summary>
        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this Channel<T> channel, bool closeWriterOnCancellation, CancellationToken cancellationToken = default)
            // note: we can't check CanBeCancelled too early here, as the CT passed to the iterator can be *different*
            => closeWriterOnCancellation ? AsAsyncEnumerableCloseWriter(channel, cancellationToken) : AsAsyncEnumerable(channel.Reader, cancellationToken);

        /// <summary>
        /// Consumes a channel as an asynchronous sequence
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this ChannelReader<T> reader, CancellationToken cancellationToken = default)
            => reader.ReadAllAsync(cancellationToken);
#endif

        private static async IAsyncEnumerable<T> AsAsyncEnumerableCloseWriter<T>(this Channel<T> channel, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            static void TryCloseWriter(object state)
            {
                try
                {
                    if (state is Channel<T> channel)
                    {
                        Console.WriteLine("completing writer");
                        channel.Writer.Complete();
                    }
                }
                catch { }
            }
            using (cancellationToken.Register(s => TryCloseWriter(s), channel, false))
            {
                while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out T item))
                    {
                        yield return item;
                    }
                }
            }
        }

        private static readonly UnboundedChannelOptions s_defaultOptions = new UnboundedChannelOptions {
            AllowSynchronousContinuations = true, SingleReader = true, SingleWriter = true };

        /// <summary>
        /// Consumes an asynchronous sequence as a channel
        /// </summary>
        public static ChannelReader<T> AsChannelReader<T>(this IAsyncEnumerable<T> sequence, ChannelOptions? channelOptions = null, CancellationToken cancellationToken = default)
        {
            Channel<T> channel;
            if (channelOptions == null)  channel = Channel.CreateUnbounded<T>(s_defaultOptions);
            else if (channelOptions is UnboundedChannelOptions uco) channel = Channel.CreateUnbounded<T>(uco);
            else if (channelOptions is BoundedChannelOptions bco) channel = Channel.CreateBounded<T>(bco);
            else ThrowInvalidOptions();

            Task.Run(() => Pump(sequence, channel.Writer, cancellationToken), cancellationToken);
            return channel.Reader;

            static void ThrowInvalidOptions() => throw new ArgumentOutOfRangeException(nameof(channelOptions));

            static async Task Pump(IAsyncEnumerable<T> sequence, ChannelWriter<T> writer, CancellationToken cancellationToken)
            {
                try
                {
                    await foreach (var value in sequence.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        while (!writer.TryWrite(value))
                        {
                            await writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                    writer.TryComplete();
                }
                catch (Exception ex)
                {
                    writer.TryComplete(ex);
                }
            }
        }
    }
}
