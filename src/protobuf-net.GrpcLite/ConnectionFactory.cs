using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net.Security;

namespace ProtoBuf.Grpc.Lite;

public static class ConnectionFactory
{
    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> ConnectNamedPipe(string pipeName, string serverName = ".", ILogger? logger = null) => async cancellationToken =>
    {
        if (string.IsNullOrWhiteSpace(serverName)) serverName = ".";
        var pipe = new NamedPipeClientStream(serverName, pipeName,
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

    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> ListenNamedPipe(string pipeName, ILogger? logger = null) => async cancellationToken =>
    {
        var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
        try
        {
            logger.Debug(pipeName, static (state, _) => $"waiting for connection... {state}");
            await pipe.WaitForConnectionAsync(cancellationToken);
            logger.Debug(pipeName, static (state, _) => $"client connected to {state}");
            return new ConnectionState<Stream>(pipe, pipeName)
            {
                Logger = logger,
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex);
            await pipe.SafeDisposeAsync();
            throw;
        }
    };

    public static Func<CancellationToken, ValueTask<ConnectionState<T>>> With<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        Func<ConnectionState<T>, ConnectionState<T>> selector)
        => async cancellationToken => selector(await factory(cancellationToken));

    internal static IFrameConnection WithThreadSafeWrite(this IFrameConnection connection)
        => connection.ThreadSafeWrite ? connection : new SynchronizedGate(connection, 0);

    public static Func<CancellationToken, ValueTask<ConnectionState<TTarget>>> With<TSource, TTarget>(
        this Func<CancellationToken, ValueTask<ConnectionState<TSource>>> factory,
        Func<TSource, TTarget> selector)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            return source.ChangeType(selector(source.Value));
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> WithGZip(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        CompressionLevel compreessionLevel = CompressionLevel.Optimal)
        => async cancellationToken =>
    {
        var source = await factory(cancellationToken);
        var pair = source.Value;
        try
        {
            source.Value = DuplexStream.Create(
                read: new GZipStream(source.Value, CompressionMode.Decompress),
                write: new GZipStream(source.Value, compreessionLevel));
            return source;
        }
        catch
        {
            await pair.SafeDisposeAsync();
            throw;
        }
    };

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
                source.Value = new SslStream(source.Value, false,
                    userCertificateValidationCallback, userCertificateSelectionCallback, encryptionPolicy);
                return source;
            }
            catch
            {
                await source.Value.SafeDisposeAsync();
                throw;
            }
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> WithFrameBuffer(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        int inputFrames, int outputFrames) => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                IFrameConnection nonGated = new StreamFrameConnection(source.Value, source.Logger),
                    gated = inputFrames > 0
                    ? new BufferedGate(nonGated, inputFrames, outputFrames)
                    : new SynchronizedGate(nonGated, outputFrames);
                return source.ChangeType<IFrameConnection>(gated);
            }
            catch
            {
                await source.Value.SafeDisposeAsync();
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
            return new LiteChannel(source.Value, source.Name, source.Logger);
        }
        catch
        {
            await source.Value.SafeDisposeAsync();
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

public sealed class ConnectionState<T>
{
    public ConnectionState(T connection, string name)
    {
        Value = connection;
        Name = name;
    }

    public string Name { get; set; }

    public T Value { get; set; }

    public ILogger? Logger { get; set; }

    public ConnectionState<TTarget> ChangeType<TTarget>(TTarget connection)
        => new ConnectionState<TTarget>(connection, Name)
        {
            Logger = Logger
        };
}
