using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

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
            logger.Information(pipeName, static (state, _) => $"client connected to {state}");
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
            try
            {
                return source.ChangeType(selector(source.Value));
            }
            catch (Exception ex)
            {
                source.Logger.Error(ex);
                if (source.Value is IAsyncDisposable disposable)
                {
                    await disposable.SafeDisposeAsync();
                }
                throw;
            }
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> WithGZip<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        CompressionLevel compreessionLevel = CompressionLevel.Optimal) where T : Stream
        => async cancellationToken =>
    {
        var source = await factory(cancellationToken);
        var pair = source.Value;
        try
        {
            return source.ChangeType(DuplexStream.Create(
                read: new GZipStream(source.Value, CompressionMode.Decompress),
                write: new GZipStream(source.Value, compreessionLevel)));
        }
        catch
        {
            await pair.SafeDisposeAsync();
            throw;
        }
    };

    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> WithTls(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        RemoteCertificateValidationCallback? userCertificateValidationCallback = null,
        LocalCertificateSelectionCallback? userCertificateSelectionCallback = null,
        EncryptionPolicy encryptionPolicy = default)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                var tls = new SslStream(source.Value, false, userCertificateValidationCallback,
                    userCertificateSelectionCallback, encryptionPolicy);
                return source.ChangeType(tls);
            }
            catch
            {
                await source.Value.SafeDisposeAsync();
                throw;
            }
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsServer(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        X509Certificate serverCertificate)
        => factory.AuthenticateAsServer(new SslServerAuthenticationOptions
        {
            ServerCertificate = serverCertificate
        });

    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsServer(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        SslServerAuthenticationOptions options)
        => async cancellationToken =>
    {
        var source = await factory(cancellationToken);
        try
        {
            source.Logger.Debug("Authenticating as server...");
            await source.Value.AuthenticateAsServerAsync(options, cancellationToken);
            source.Logger.Debug("Authenticated");
            return source;
        }
        catch
        {
            await source.Value.SafeDisposeAsync();
            throw;
        }
    };

    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsClient(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        string targetHost)
        => factory.AuthenticateAsClient(new SslClientAuthenticationOptions
        {
             TargetHost = targetHost
        });

    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsClient(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        SslClientAuthenticationOptions options)
        => async cancellationToken =>
    {
        var source = await factory(cancellationToken);
        try
        {
            source.Logger.Debug("Authenticating as client...");
            await source.Value.AuthenticateAsClientAsync(options, cancellationToken);
            source.Logger.Debug("Authenticated");
            return source;
        }
        catch
        {
            await source.Value.SafeDisposeAsync();
            throw;
        }
    };

    public static Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> AsFrames<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        bool mergeWrites = true) where T : Stream
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                return source.ChangeType<IFrameConnection>(new StreamFrameConnection(source.Value, mergeWrites, source.Logger));
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
        return await CreateChannelAsync(factory, cts.Token);
    }

    public async static ValueTask<LiteChannel> CreateChannelAsync(
    this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
    TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        return await CreateChannelAsync(factory.AsFrames(), cts.Token);
    }

    public static ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        CancellationToken cancellationToken = default)
        => CreateChannelAsync(factory.AsFrames(), cancellationToken);

    public static async ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> factory,
        CancellationToken cancellationToken = default)
    {
        var source = await factory(cancellationToken);
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
