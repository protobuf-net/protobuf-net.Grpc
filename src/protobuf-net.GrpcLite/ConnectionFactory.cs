using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite;

/// <summary>
/// Provides utility methods for constructing gRPC connections.
/// </summary>
public static class ConnectionFactory
{
    /// <summary>
    /// Connect (as a client) to a named-pipe server.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> ConnectNamedPipe(string pipeName, string serverName = ".", ILogger? logger = null) => async cancellationToken =>
    {
        if (string.IsNullOrWhiteSpace(serverName)) serverName = ".";
        var pipe = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
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

    /// <summary>
    /// Listen (as a server) to a named-pipe.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<Stream>>> ListenNamedPipe(string pipeName, ILogger? logger = null) => async cancellationToken =>
    {
        var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough);
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

    /// <summary>
    /// Connect (as a client) to a socket server.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<Socket>>> ConnectSocket(EndPoint endpoint, ILogger? logger = null) => async cancellationToken =>
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        try
        {
            await socket.ConnectAsync(endpoint);
            return new ConnectionState<Socket>(socket, endpoint.ToString() ?? "")
            {
                Logger = logger,
            };
        }
        catch
        {
            socket.SafeDispose();
            throw;
        }
    };

    /// <summary>
    /// Listen (as a server) to a socket.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<Socket>>> ListenSocket(EndPoint endpoint, ILogger? logger = null)
    {
        var listener = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(endpoint);
        listener.Listen(backlog: 10);
        logger.Debug(endpoint, static (state, _) => $"Listening for socket connections from {state}...");
        return async cancellationToken =>
        {
            logger.Information(endpoint, static (state, _) => $"waiting for connection... {state}");
            var socket = await listener.AcceptAsync();
            if (socket is null) return null!;
            var name = socket.LocalEndPoint?.ToString() ?? "";
            socket.NoDelay = true;
            logger.Information(name, static (state, _) => $"client connected to {state}");
            return new ConnectionState<Socket>(socket, name)
            {
                Logger = logger,
            };
        };
    }

    /// <summary>
    /// Applies a selector to the connection.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<T>>> With<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        Func<ConnectionState<T>, ConnectionState<T>> selector)
        => async cancellationToken => selector(await factory(cancellationToken));

    /// <summary>
    /// Applies a selector to the connection.
    /// </summary>
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

    /// <summary>
    /// Performs bidirectional gzip compression/decompression to the connection.
    /// </summary>
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

    /// <summary>
    /// Creates a TLS wrapper over the connection.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> WithTls<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        RemoteCertificateValidationCallback? userCertificateValidationCallback = null,
        LocalCertificateSelectionCallback? userCertificateSelectionCallback = null,
        EncryptionPolicy encryptionPolicy = default) where T: Stream
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

#if NET472
    /// <summary>
    /// Authenticates the connection as a server.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsServer(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        X509Certificate serverCertificate, bool clientCertificateRequired = false, bool checkCertificateRevocation = false) => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                // TODO: support cancellation
                source.Logger.Debug("Authenticating as server...");
                await source.Value.AuthenticateAsServerAsync(serverCertificate, clientCertificateRequired, checkCertificateRevocation);
                source.Logger.Debug(source.Value, static (state, _) => $"Authenticated; server: '{state?.LocalCertificate?.Subject}', client: '{state?.RemoteCertificate?.Subject}'");
                return source;
            }
            catch
            {
                await source.Value.SafeDisposeAsync();
                throw;
            }
        };

    /// <summary>
    /// Authenticates the connection as a client.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsClient(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        string targetHost)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                // TODO: support cancellation
                source.Logger.Debug("Authenticating as client...");
                await source.Value.AuthenticateAsClientAsync(targetHost);
                source.Logger.Debug(source.Value, static (state, _) => $"Authenticated; server: '{state?.RemoteCertificate?.Subject}', client: '{state?.LocalCertificate?.Subject}'");

                return source;
            }
            catch
            {
                await source.Value.SafeDisposeAsync();
                throw;
            }
        };

#else
    /// <summary>
    /// Authenticates the connection as a server.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsServer(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        X509Certificate serverCertificate, bool clientCertificateRequired = false, bool checkCertificateRevocation = false)
        => factory.AuthenticateAsServer(new SslServerAuthenticationOptions
        {
            ServerCertificate = serverCertificate,
            ClientCertificateRequired = clientCertificateRequired,
            CertificateRevocationCheckMode = checkCertificateRevocation ? X509RevocationMode.Offline : X509RevocationMode.NoCheck,
        });

    /// <summary>
    /// Authenticates the connection as a server.
    /// </summary>
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
            source.Logger.Debug(source.Value, static (state, _) => $"Authenticated; server: '{state?.LocalCertificate?.Subject}', client: '{state?.RemoteCertificate?.Subject}'");
            return source;
        }
        catch
        {
            await source.Value.SafeDisposeAsync();
            throw;
        }
    };

    /// <summary>
    /// Authenticates the connection as a client.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> AuthenticateAsClient(
        this Func<CancellationToken, ValueTask<ConnectionState<SslStream>>> factory,
        string targetHost, RemoteCertificateValidationCallback? remoteCertificateValidationCallback = null)
        => factory.AuthenticateAsClient(new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            RemoteCertificateValidationCallback = remoteCertificateValidationCallback,
        });

    /// <summary>
    /// Authenticates the connection as a client.
    /// </summary>
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
            source.Logger.Debug(source.Value, static (state, _) => $"Authenticated; server: '{state?.RemoteCertificate?.Subject}', client: '{state?.LocalCertificate?.Subject}'");
            return source;
        }
        catch
        {
            await source.Value.SafeDisposeAsync();
            throw;
        }
    };
#endif

    /// <summary>
    /// Creates a <see cref="Frame"/> processor over a <see cref="Stream"/>.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> AsFrames<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        bool mergeWrites = false, int outputBufferSize = -1) where T : Stream
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                return source.ChangeType<IFrameConnection>(new StreamFrameConnection(source.Value, mergeWrites, outputBufferSize, source.Logger));
            }
            catch
            {
                await source.Value.SafeDisposeAsync();
                throw;
            }
        };

    /// <summary>
    /// Creates a <see cref="Frame"/> processor over a <see cref="Stream"/>.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<NetworkStream>>> AsStream(
        this Func<CancellationToken, ValueTask<ConnectionState<Socket>>> factory)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                return source.ChangeType(new NetworkStream(source.Value));
            }
            catch
            {
                source.Value.SafeDispose();
                throw;
            }
        };

    /// <summary>
    /// Creates a <see cref="Frame"/> processor over a <see cref="Stream"/>.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> AsFrames(
        this Func<CancellationToken, ValueTask<ConnectionState<Socket>>> factory)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            try
            {
                return source.ChangeType<IFrameConnection>(new SocketFrameConnection(source.Value, -1, source.Logger));
            }
            catch
            {
                source.Value.SafeDispose();
                throw;
            }
        };

    /// <summary>
    /// Specified an <see cref="ILogger"/> to use with the connection.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<T>>> Log<T>(
            this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
            ILogger? logger) => factory.With(source =>
            {
                source.Logger = logger;
                return source;
            });

    /// <summary>
    /// Creates a <see cref="LiteChannel"/> (client) over a connection.
    /// </summary>
    public async static ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> factory,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        return await CreateChannelAsync(factory, cts.Token);
    }

    /// <summary>
    /// Creates a <see cref="LiteChannel"/> (client) over a connection.
    /// </summary>
    public async static ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        return await CreateChannelAsync(factory.AsFrames(), cts.Token);
    }

    /// <summary>
    /// Creates a <see cref="LiteChannel"/> (client) over a connection.
    /// </summary>
    public static ValueTask<LiteChannel> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<Stream>>> factory,
        CancellationToken cancellationToken = default)
        => CreateChannelAsync(factory.AsFrames(), cancellationToken);

    /// <summary>
    /// Creates a <see cref="LiteChannel"/> (client) over a connection.
    /// </summary>
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

/// <summary>
/// Represents the state of a connection being constructed
/// </summary>
public sealed class ConnectionState<T>
{
    /// <summary>
    /// Create a new <see cref="ConnectionState{T}"/> instance.
    /// </summary>
    public ConnectionState(T connection, string name)
    {
        Value = connection;
        Name = name;
    }

    /// <summary>
    /// The name (used in <see cref="ChannelBase.Target"/>) of this connection.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The connection being constructed.
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// The logging endpoint for this connection.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Create a new instance for a different connection type, preserving the other configured options.
    /// </summary>
    public ConnectionState<TTarget> ChangeType<TTarget>(TTarget connection)
        => new ConnectionState<TTarget>(connection, Name)
        {
            Logger = Logger
        };
}
