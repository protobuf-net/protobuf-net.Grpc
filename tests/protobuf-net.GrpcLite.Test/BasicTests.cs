using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace protobuf_net.GrpcLite.Test;
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
    public void CanCreateInvoker()
    {
        using var channel = new StreamChannel(Stream.Null, Stream.Null, Me());
        var invoker = channel.CreateCallInvoker();
    }


    [Fact]
    public async Task CanWriteAndParseFrame()
    {
        var ms = new MemoryStream();
        var channel = Channel.CreateUnbounded<StreamFrame>();
        await channel.Writer.WriteAsync(StreamFrame.GetInitializeFrame(FrameKind.NewUnary, 42, 0, "/myservice/mymethod", ""));
        var bytes = Encoding.UTF8.GetBytes("hello, world!");
        await channel.Writer.WriteAsync(new StreamFrame(FrameKind.Payload, 42, (byte)PayloadFlags.EndItem, bytes, 0, (ushort)bytes.Length, FrameFlags.None, sequenceId: 3));
        channel.Writer.Complete();
        await StreamFrame.WriteFromOutboundChannelToStream(channel, ms, Logger, default);

        var hex = GetHex(ms);
        Assert.Equal(
            "01-00-2A-00-00-00-13-00-" // unary, id 42, length 19
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "05-01-2A-00-03-00-0D-00-" // payload, final, id 42, length 13, seq 3
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21", hex); // "hello, world!"

        ms.Position = 0;
        using (var frame = await StreamFrame.ReadAsync(ms, CancellationToken.None))
        {
            Assert.Equal(FrameKind.NewUnary, frame.Kind);
            Assert.Equal(0, frame.KindFlags);
            Assert.Equal(42, frame.RequestId);
            Assert.Equal(0, frame.SequenceId);
            Assert.Equal(19, frame.Length);
            Assert.Equal("/myservice/mymethod", Encoding.UTF8.GetString(frame.Buffer, frame.Offset, frame.Length));
        }
        using (var frame = await StreamFrame.ReadAsync(ms, CancellationToken.None))
        {
            Assert.Equal(FrameKind.Payload, frame.Kind);
            Assert.Equal((byte)PayloadFlags.EndItem, frame.KindFlags);
            Assert.Equal(42, frame.RequestId);
            Assert.Equal(3, frame.SequenceId);
            Assert.Equal(13, frame.Length);
            Assert.Equal("hello, world!", Encoding.UTF8.GetString(frame.Buffer, frame.Offset, frame.Length));
        }
    }

    static readonly Marshaller<string> StringMarshaller_Simple = new Marshaller<string>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);

    static string GetHex(MemoryStream stream)
    {
        if (!stream.TryGetBuffer(out var buffer)) buffer = stream.ToArray();
        return BitConverter.ToString(buffer.Array!, buffer.Offset, buffer.Count);
    }

    [Fact]
    public async Task CanWriteSimpleAsyncMessage()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var ms = new MemoryStream();
        await using var channel = new StreamChannel(Stream.Null, ms, Me());
        var invoker = channel.CreateCallInvoker();
        using var call = invoker.AsyncUnaryCall(new Method<string, string>(MethodType.Unary, "myservice", "mymethod", StringMarshaller_Simple, StringMarshaller_Simple), "",
            default(CallOptions).WithCancellationToken(cts.Token), "hello, world!");

        // we don't expect a reply
        var oce = await Assert.ThrowsAsync<TaskCanceledException>(() => call.ResponseAsync);
        Assert.Equal(cts.Token, oce.CancellationToken);

        var hex = GetHex(ms);
        Assert.Equal(
            "01-00-00-00-00-00-13-00-" // unary, id 0/0, length 19
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "05-03-00-00-01-00-0D-00-" // payload, final chunk, final element, id 0/1, length 13
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21", hex); // "hello, world!"
    }

    [Fact]
    public async Task CanWriteSimpleSyncMessage()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var ms = new MemoryStream();
        await using var channel = new StreamChannel(Stream.Null, ms, Me());
        var invoker = channel.CreateCallInvoker();

        // we don't expect a reply
        var oce = Assert.Throws<TaskCanceledException>(() => invoker.BlockingUnaryCall(new Method<string, string>(MethodType.Unary, "myservice", "mymethod", StringMarshaller_Simple, StringMarshaller_Simple), "",
            default(CallOptions).WithCancellationToken(cts.Token), "hello, world!"));
        Assert.Equal(cts.Token, oce.CancellationToken);

        var hex = GetHex(ms);
        Assert.Equal(
            "01-00-00-00-00-00-13-00-" // unary, id 0/0, length 19
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "05-03-00-00-01-00-0D-00-" // payload, final chunk, final element, id 0/1 length 13
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21", hex); // "hello, world!"
    }

    [Fact]
    public void CanBindService()
    {
        var server = new TestStreamServer(Logger);
        server.AddConnection(Stream.Null, Stream.Null, CancellationToken.None);
        server.ManualBind<MyService>();
        Assert.Equal(2, server.MethodCount);
    }

    [Fact]
    public void CanCreateTestFixture()
    {
        using var obj = new TestServerHost();
    }

    class TestStreamServer : StreamServer
    {
        public TestStreamServer(ILogger? logger) : base(logger) { }
    }



}

