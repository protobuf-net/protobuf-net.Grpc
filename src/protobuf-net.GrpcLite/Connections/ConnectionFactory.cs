using ProtoBuf.Grpc.Lite.Internal;
using System.IO.Pipes;
using System.IO.Compression;
using System.Net.Security;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace ProtoBuf.Grpc.Lite.Connections;

public static class ConnectionFactory
{
    public static Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> ConnectNamedPipe(string pipeName, string serverName = ".", ILogger? logger = null) => async cancellationToken =>
        {
            var pipe = new NamedPipeClientStream(pipeName, serverName,
                PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            try
            {
                await pipe.ConnectAsync(cancellationToken);
                return new ConnectionState<StreamPair>(new StreamPair(pipe), pipeName)
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
    
    public static Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> WithTls(
        this Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> factory,
        RemoteCertificateValidationCallback? userCertificateValidationCallback = null,
        LocalCertificateSelectionCallback? userCertificateSelectionCallback = null,
        EncryptionPolicy encryptionPolicy = default)
        => async cancellationToken =>
        {
            var source = await factory(cancellationToken);
            var pair = source.Connection;
            try
            {
                if (!ReferenceEquals(pair.Input, pair.Output))
                {
                    throw new InvalidOperationException("TLS requires a duplex stream");
                }
                ;
                source.Connection = new StreamPair(new SslStream(pair.Input, false,
                    userCertificateValidationCallback, userCertificateSelectionCallback, encryptionPolicy));
                return source;
            }
            catch
            {
                await pair.SafeDisposeAsync();
                throw;
            }
        };

    public static Func<CancellationToken, ValueTask<ConnectionState<T>>> Buffer<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        int inputFrames, int outputFrames) => factory.With(source =>
        {
            source.InputBuffer = inputFrames;
            source.OutputBuffer = outputFrames;
            return source;
        });

    public static Func<CancellationToken, ValueTask<ConnectionState<T>>> Log<T>(
        this Func<CancellationToken, ValueTask<ConnectionState<T>>> factory,
        ILogger? logger) => factory.With(source =>
        {
            source.Logger = logger;
            return source;
        });

    public static async ValueTask<ChannelBase> CreateChannelAsync(
        this Func<CancellationToken, ValueTask<ConnectionState<StreamPair>>> factory,
        CancellationToken cancellationToken = default)
    {
        var source = await factory(cancellationToken);
        var pair = source.Connection;
        try
        {
            IGatedTerminator terminator = new StreamTerminator(pair.Input, pair.Output).Gate(source.InputBuffer, source.OutputBuffer);
            return new LiteChannel(pair.Input, pair.Output, source.Name, source.Logger);
        }
        catch
        {
            await pair.SafeDisposeAsync();
            throw;
        }
    }
}

static class Demo
{
    static async ValueTask Usage()
    {
        await Task.Delay(20);
        var channel = ConnectionFactory.ConnectNamedPipe("foo").WithTls().Buffer(0, 0).CreateChannelAsync();
    }
}

internal interface ITerminator : IEnumerable<Frame>
{
    ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken);
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
    public int InputBuffer { get; set; }
    /// <summary>The size of the output buffer, in frames</summary>
    public int OutputBuffer { get; set; }
    public ILogger? Logger { get; set; }
}
