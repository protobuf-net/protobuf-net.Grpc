using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.GrpcLite.Test;

[SetLoggingSource]
public class BasicTests
{
    private readonly ITestOutputHelper _output;
    public BasicTests(ITestOutputHelper output)
    {
        _output = output;
        Logger = _output.CreateLogger("");
    }

    ILogger Logger { get; }

    static string Me([CallerMemberName] string caller = "") => caller;

    [Fact]
    public async ValueTask CanCreateInvoker()
    {
        await using var channel = new LiteChannel(new StreamFrameConnection(Stream.Null), Me());
        var invoker = channel.CreateCallInvoker();
    }

    class DummyStream : LiteStream<string, string>
    {
        class SimpleMethod : IMethod
        {
            public SimpleMethod(string fullName) => FullName = fullName;
            MethodType IMethod.Type => MethodType.Unary;

            string IMethod.ServiceName => throw new NotImplementedException();

            string IMethod.Name => throw new NotImplementedException();

            public string FullName { get; }
        }
        public DummyStream(string fullName, ushort id)
            : base(new SimpleMethod(fullName), null!, null)
        {
            Id = id;
        }

        protected override bool IsClient => true;

        protected override Action<string, SerializationContext> Serializer => throw new NotImplementedException();
        protected override Func<DeserializationContext, string> Deserializer => throw new NotImplementedException();
    }

    private static string GetHex(ReadOnlyMemory<byte> buffer)
    {
        if (!MemoryMarshal.TryGetArray(buffer, out var segment))
        {
            segment = buffer.ToArray();
        }
        return BitConverter.ToString(segment.Array!, segment.Offset, segment.Count);
    }

    [Fact]
    public void CanWriteAndParseFrame()
    {
        var stream = new DummyStream("/myservice/mymethod", 42);

        var ctx = PayloadFrameSerializationContext.Get(stream, RefCountedMemoryPool<byte>.Shared, FrameKind.StreamHeader, 0);
        try
        {
            MetadataEncoder.WriteHeader(ctx, true, ((IStream)stream).Method, null, default);
            ctx.Complete();
            var frame = ctx.PendingFrames.Single();
            var hex = GetHex(frame.Memory);
            Assert.Equal(
        "02-00-2A-00-00-00-13-80-" // unary, id 42, length 19, final
        + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64", hex); // "/myservice/mymethod"

            frame = new Frame(frame.Memory);
            var header = frame.GetHeader();
            Assert.Equal(FrameKind.StreamHeader, header.Kind);
            Assert.Equal(FrameFlags.None, header.Flags);
            Assert.Equal(42, header.StreamId);
            Assert.Equal(0, header.SequenceId);
            Assert.Equal(19, header.PayloadLength);
            Assert.Equal("/myservice/mymethod", Encoding.UTF8.GetString(frame.GetPayload().Span));

            frame.Release();
        }
        finally
        {
            ctx.Recycle();
        }

    }

    static readonly Marshaller<string> StringMarshaller_Simple = new Marshaller<string>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);

    static string GetHex(MemoryStream stream)
    {
        if (!stream.TryGetBuffer(out var buffer)) buffer = stream.ToArray();
        return BitConverter.ToString(buffer.Array!, buffer.Offset, buffer.Count);
    }

    private IFrameConnection CreateClientPipe(out IDuplexPipe server)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var client = new DuplexPipe(serverToClient.Reader, clientToServer.Writer);
        server = new DuplexPipe(clientToServer.Reader, serverToClient.Writer);
        return new PipeFrameConnection(client, Logger);
    }
    sealed class DuplexPipe : IDuplexPipe
    {
        public DuplexPipe(PipeReader input, PipeWriter output)
        {
            Input = input;
            Output = output;
        }
        public PipeReader Input { get; }

        public PipeWriter Output { get; }
    }

    [Fact]
    public async Task CanWriteSimpleAsyncMessage()
    {
        var pipe = CreateClientPipe(out var server);
        using var cts = Timeout();
        await using var channel = new LiteChannel(pipe, Me(), Logger);
        var invoker = channel.CreateCallInvoker();
        using var call = invoker.AsyncUnaryCall(new Method<string, string>(MethodType.Unary, "myservice", "mymethod", StringMarshaller_Simple, StringMarshaller_Simple), "",
            default(CallOptions).WithCancellationToken(cts.Token), "hello, world!");

        // we don't expect a reply
        var oce = await Assert.ThrowsAsync<OperationCanceledException>(() => call.ResponseAsync);
        Assert.Equal(cts.Token, oce.CancellationToken);
        await Task.Delay(100);
        await pipe.SafeDisposeAsync();
        var hex = await GetHexAsync(server);
        Assert.StartsWith(
            "02-01-00-00-00-00-13-80-" // unary, id 0/0, length 19 (final)
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "03-01-00-00-01-00-0D-80-" // payload, id 0/1, length 13 (final)
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21-"
            + "04-00-00-00-02-00-00-80", hex); // trailer, 0/2, empty (final)
    }

    private async ValueTask<string> GetHexAsync(IDuplexPipe pipe)
    {
        using var ms = new MemoryStream();
        await pipe.Input.CopyToAsync(ms);
        return GetHex(ms);
    }

    const int DEBUG_TIMEOPUT_MULTIPLIER = 1; // 10;
    private CancellationTokenSource Timeout(int seconds = 2)
        => new CancellationTokenSource(TimeSpan.FromSeconds(Debugger.IsAttached ? seconds * DEBUG_TIMEOPUT_MULTIPLIER : seconds));

    [Fact]
    public async Task CanWriteSimpleSyncMessage()
    {
        var pipe = CreateClientPipe(out var server);
        using var cts = Timeout();
        await using var channel = new LiteChannel(pipe, Me(), Logger);
        var invoker = channel.CreateCallInvoker();

        // we don't expect a reply
        var oce = Assert.Throws<OperationCanceledException>(() => invoker.BlockingUnaryCall(new Method<string, string>(MethodType.Unary, "myservice", "mymethod", StringMarshaller_Simple, StringMarshaller_Simple), "",
            default(CallOptions).WithCancellationToken(cts.Token), "hello, world!"));
        Assert.Equal(cts.Token, oce.CancellationToken);
        await Task.Delay(100);
        Logger.Information("disposing...");
        await pipe.SafeDisposeAsync();
        Logger.Information("disposed");

        var hex = await GetHexAsync(server);
        Assert.StartsWith(
            "02-01-00-00-00-00-13-80-" // unary, id 0/0, length 19 (final)
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "03-01-00-00-01-00-0D-80-" // payload, id 0/1, length 13 (final)
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21-"
            + "04-00-00-00-02-00-00-80", hex); // trailer, 0/2, empty (final)
    }

    [Fact]
    public void CanBindService()
    {
        var server = new LiteServer(Logger);
        server.Bind<MyService>();
        Assert.Equal(4, server.MethodCount);
    }

    [Fact]
    public void CanCreateTestFixture()
    {
        using var obj = new TestServerHost();
    }

    [Fact]
    public void CanParseMultiFrame()
    {
        const string hex = "02-00-EB-02-00-00-11-80-2F-46-6F-6F-53-65-72-76-69-63-65-2F-55-6E-61-72-79-03-00-EB-02-01-00-03-80-08-EA-05-04-00-EB-02-02-00-00-80";
        var builder = Frame.CreateBuilder();
        var data = Convert.FromHexString(hex.Replace("-", ""));
        data.CopyTo(builder.GetBuffer()); // we'll just assume that this has capacity!
        int bytesRead = data.Length;
        Assert.Equal(44, bytesRead);
        Assert.True(builder.TryRead(ref bytesRead, out var frame), "first");
        Assert.Equal(25, frame.TotalLength);
        Assert.Equal(19, bytesRead);

        Assert.True(builder.TryRead(ref bytesRead, out frame), "second");
        Assert.Equal(11, frame.TotalLength);
        Assert.Equal(8, bytesRead);

        Assert.True(builder.TryRead(ref bytesRead, out frame), "third");
        Assert.Equal(8, frame.TotalLength);
        Assert.Equal(0, bytesRead);

        Assert.False(builder.TryRead(ref bytesRead, out frame), "fourth");
        Assert.Equal(0, bytesRead);
    }

}

