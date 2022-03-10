using Grpc.Core;
using ProtoBuf.Grpc.Lite.Internal;
using System.Buffers;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite;

public class StreamChannel : ChannelBase
{
    private readonly Stream _input, _output;
    public static async ValueTask<StreamChannel> ForNamedPipe(
        string pipeName, string serverName = ".",
        CancellationToken cancellationToken = default)
    {
        var client = new NamedPipeClientStream(serverName, pipeName,
                PipeDirection.InOut, PipeOptions.WriteThrough | PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                TokenImpersonationLevel.None, HandleInheritability.None);
        try
        {
            await client.ConnectAsync(cancellationToken);
            var target = serverName == "." ? pipeName : (serverName + "/" + pipeName);
            return new StreamChannel(client, client, target);
        }
        catch
        {
            try { await client.DisposeAsync(); } catch { } // best efforts
            throw;
        }
    }

    readonly Channel<StreamFrame> _outbound;
    readonly CallInvoker _callInvoker;

    private static readonly UnboundedChannelOptions OutboundOptions = new()
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true,
    };

    public StreamChannel(Stream duplexStream, string target) : this(duplexStream, duplexStream, target)
    { }

    public StreamChannel(Stream input, Stream output, string target) : base(target)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!input.CanRead) throw new ArgumentException("Cannot read from input stream", nameof(input));
        if (!output.CanWrite) throw new ArgumentException("Cannot write to output stream", nameof(output));
        _input = input;
        _output = output;
        _outbound = Channel.CreateUnbounded<StreamFrame>(OutboundOptions);
        _callInvoker = new StreamCallInvoker(_outbound);
        Writer = WriteFromOutboundChannelToStream();
    }

    private async Task WriteFromOutboundChannelToStream()
    {
        CancellationToken ct = CancellationToken.None; // easier to add now than later!
        await Task.Yield(); // ensure we don't block the constructor
        byte[]? headerBuffer = null;
        try
        {
            while (await _outbound.Reader.WaitToReadAsync(ct))
            {
                while (_outbound.Reader.TryRead(out var frame))
                {
                    var frameFlags = frame.FrameFlags;
                    if ((frameFlags & FrameFlags.HeaderReserved) != 0)
                    {
                        // we can write the header into the existing buffer, and use a single write
                        var offset = frame.Offset - StreamFrame.HeaderBytes;
                        frame.Write(frame.Buffer, offset);
                        await _output.WriteAsync(frame.Buffer, offset, frame.Length + StreamFrame.HeaderBytes, ct);
                    }
                    else
                    {
                        // use a scratch-buffer for the header, and write the header and payload separately
                        frame.Write(headerBuffer ??= ArrayPool<byte>.Shared.Rent(StreamFrame.HeaderBytes), 0);
                        await _output.WriteAsync(headerBuffer, 0, StreamFrame.HeaderBytes, ct);
                        if (frame.Length != 0)
                        {
                            await _output.WriteAsync(frame.Buffer, frame.Offset, frame.Length, ct);
                        }
                    }

                    if ((frameFlags & FrameFlags.RecycleBuffer) != 0)
                    {
                        ArrayPool<byte>.Shared.Return(frame.Buffer);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // block the writer, since we're doomed
            _outbound.Writer.TryComplete(ex);
        }
        finally
        {
            if (headerBuffer is not null) ArrayPool<byte>.Shared.Return(headerBuffer);
        }
    }

    public Task Writer { get; }

    public override CallInvoker CreateCallInvoker() => _callInvoker;

    protected override Task ShutdownAsyncCore()
    {
        if (ReferenceEquals(_input, _output))
        {
            return _input is null ? Task.CompletedTask : _input.DisposeAsync().AsTask();
        }
        else
        {
            return SlowPath(_input, _output).AsTask();
        }
        async ValueTask SlowPath(Stream read, Stream write)
        {
            if (read != null) await read.DisposeAsync();
            if (write != null) await write.DisposeAsync();
        }
    }
}
