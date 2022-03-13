using ProtoBuf.Grpc.Lite.Internal;
using System.IO.Pipes;
using System.IO.Compression;
using System.Net.Security;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;

namespace ProtoBuf.Grpc.Lite.Connections;

public static class ConnectionFactory
{
    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> ConnectNamedPipe(string pipeName, string serverName = ".", ILogger? logger = null) => async cancellationToken =>
        {
            var pipe = new NamedPipeClientStream(pipeName, serverName,
                PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            try
            {
                await pipe.ConnectAsync(cancellationToken);
                return new ConnectionState<Stream>(pipe, pipeName)
                {
                    Logger = logger,
                };
            }
            catch
            {
                await pipe.SafeDisposeAsync();
                throw;
            }
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<T>>> With<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        Func<ConnectionState<T>, ConnectionState<T>> selector)
        => async cancellationToken => selector(await factory(cancellationToken));

    public static Func<CancellationToken, ValueTask<ConnectionState<TTarget>>> With<TSource, TTarget>(
        this Func<CancellationToken, ValueTask<ConnectionState<TSource>>> factory,
        Func<TSource, TTarget> selector)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            return source.ChangeType(selector(source.Connection));
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> WithGZip(
        this Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> factory,
        CompressionLevel compreessionLevel = CompressionLevel.Optimal)
        => async cancellationToken =>
    {
        var source = await factory(cancellationToken);
        var pair = source.Connection;
        try
        {
            source.Connection = new StreamPair(
                input: new GZipStream(pair.Input, CompressionMode.Decompress),
                output: new GZipStream(pair.Output, compreessionLevel));
            return source;
        }
        catch
        {
            await pair.SafeDisposeAsync();
            throw;
        }
    };

    public static Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> SplitDuplex(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory)
        => With(factory, static duplex => new StreamPair(duplex));

    public static Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> WithGZip(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        CompressionLevel compreessionLevel = CompressionLevel.Optimal)
        => factory.SplitDuplex().WithGZip();

    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> WithTls(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        RemoteCertificateValidationCallback? userCertificateValidationCallback = null,
        LocalCertificateSelectionCallback? userCertificateSelectionCallback = null,
        EncryptionPolicy encryptionPolicy = default)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                source.Connection = new SslStream(source.Connection, false,
                    userCertificateValidationCallback, userCertificateSelectionCallback, encryptionPolicy);
                return source;
            }
            catch
            {
                await source.Connection.SafeDisposeAsync();
                throw;
            }
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> WithFrameBuffer(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        int inputFrames, int outputFrames) => factory.SplitDuplex().WithFrameBuffer(inputFrames, outputFrames);

    public static Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> WithFrameBuffer(
        this Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> factory,
        int inputFrames, int outputFrames) => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                IFrameConnection nonGated = new StreamFrameConnection(source.Connection.Input, source.Connection.Output),
                    gated = inputFrames > 0
                    ? new BufferedGate(nonGated, inputFrames, outputFrames)
                    : new SynchronizedGate(nonGated, outputFrames);
                return source.ChangeType<IFrameConnection>(gated);
            }
            catch
            {
                await source.Connection.SafeDisposeAsync();
                throw;
            }
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<T>>> Log<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        ILogger? logger) => factory.With(source =>
        {
            source.Logger = logger;
            return source;
        });

    public async static ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> factory,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        return await CreateChannel(factory(cts.Token));
    }

    public async static ValueTask<LiteChannel> CreateChannelAsync(
    this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
    TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        return await CreateChannel(factory.WithFrameBuffer(0, 0)(cts.Token));
    }

    public static ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        CancellationToken cancellationToken = default)
        => factory.WithFrameBuffer(0, 0).CreateChannelAsync();

    private static async ValueTask<LiteChannel> CreateChannel(ValueTask<ConnectionState<IFrameConnection>> pending)
    {
        var source = await pending;
        try
        {
            return new LiteChannel(source.Connection, source.Name, source.Logger);
        }
        catch
        {
            await source.Connection.SafeDisposeAsync();
            throw;
        }

    }
    public static ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> factory,
        CancellationToken cancellationToken = default)
        => CreateChannel(factory(cancellationToken));
}

static class Demo
{
    static async ValueTask Usage()
    {
        await Task.Delay(20);
        ChannelBase simple = await ConnectionFactory.ConnectNamedPipe("foo").CreateChannelAsync();
        ChannelBase nuanced = await ConnectionFactory.ConnectNamedPipe("foo").WithTls().WithGZip().WithFrameBuffer(0, 0).CreateChannelAsync();
    }
}

public interface IFrameConnection : IAsyncEnumerable<NewFrame>, IAsyncDisposable
{
    ValueTask WriteAsync(BufferSegment frame, CancellationToken cancellationToken);
    bool ThreadSafeWrite { get; }
}


public readonly struct StreamPair
{
    internal ValueTask SafeDisposeAsync() => Utilities.SafeDisposeAsync(Input, Output);
    public StreamPair(Stream input, Stream output)
    {
        Input = input;
        Output = output;
    }
    public StreamPair(Stream duplex)
    {
        Input = Output = duplex;
    }
    public Stream Input { get; }
    public Stream Output { get; }
}
public sealed class ConnectionState<T>
{
    public ConnectionState(T connection, string name)
    {
        Connection = connection;
        Name = name;
    }

    public string Name { get; set; }

    public T Connection { get; set; }
    /// <summary>The size of the input buffer, in frames</summary>
    public ILogger? Logger { get; set; }

    internal ConnectionState<TTarget> ChangeType<TTarget>(TTarget connection)
        => new ConnectionState<TTarget>(connection, Name)
        {
            Logger = Logger
        };
}
