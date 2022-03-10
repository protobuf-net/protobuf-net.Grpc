using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite;

public class StreamChannel : ChannelBase, IAsyncDisposable, IDisposable
{
    public static async ValueTask<StreamChannel> ConnectNamedPipeAsync(string pipeName, string? serverName = null, CancellationToken cancellationToken = default)
    {
        string target;
        if (string.IsNullOrWhiteSpace(serverName))
        {
            serverName = ".";
            target = pipeName;
        }
        else
        {
            target = serverName + "/" + pipeName;
        }
        var client = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough,
            TokenImpersonationLevel.None, HandleInheritability.None);
        try
        {
            await client.ConnectAsync(cancellationToken);
            return new StreamChannel(client, target);
        }
        catch
        {
            try { await client.DisposeAsync(); }
            catch { }
            throw;
        }
    }

    private readonly Stream _input, _output;
    readonly Channel<StreamFrame> _outbound;
    readonly StreamCallInvoker _callInvoker;

    public StreamChannel(Stream duplexStream, string target, ILogger? logger = null, CancellationToken cancellationToken = default) : this(duplexStream, duplexStream, target, logger, cancellationToken)
    { }

    public StreamChannel(Stream input, Stream output, string target, ILogger? logger = null, CancellationToken cancellationToken = default) : base(target)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!input.CanRead) throw new ArgumentException("Cannot read from input stream", nameof(input));
        if (!output.CanWrite) throw new ArgumentException("Cannot write to output stream", nameof(output));
        _input = input;
        _output = output;
        _outbound = StreamFrame.CreateChannel();
        _callInvoker = new StreamCallInvoker(_outbound);
        Complete = StreamFrame.WriteFromOutboundChannelToStream(_outbound, _output, logger, cancellationToken);

        _ = _callInvoker.ConsumeAsync(input, logger, cancellationToken);
    }

    public Task Complete { get; }

    public override CallInvoker CreateCallInvoker() => _callInvoker;

    protected override Task ShutdownAsyncCore() => DisposeAsync().AsTask();

    public void Dispose()
    {
        _outbound.Writer.TryComplete();
        Dispose(_input, _output);
    }

    public ValueTask DisposeAsync()
    {
        _outbound.Writer.TryComplete();
        return DisposeAsync(_input, _output);
    }

    internal static void Dispose(Stream input, Stream output)
    {
        if (ReferenceEquals(input, output))
        {
            try { input?.Dispose(); } catch { }
        }
        else
        {
            try { input?.Dispose(); } catch { }
            try { output?.Dispose(); } catch { }
        }
    }
    internal static ValueTask DisposeAsync(Stream input, Stream output)
    {
        if (ReferenceEquals(input, output))
        {
            if (input is null) return default;
            var result = input.DisposeAsync();
            if (result.IsCompletedSuccessfully) return result;

            return Swallow(result);
        }
        else
        {
            return SlowPath(input, output);
        }
        static async ValueTask SlowPath(Stream input, Stream output)
        {
            if (input != null) try { await input.DisposeAsync(); } catch { }
            if (output != null) try { await output.DisposeAsync(); } catch { }
        }

        static async ValueTask Swallow(ValueTask pending)
        {
            try { await pending; } catch { }
        }
    }
}
