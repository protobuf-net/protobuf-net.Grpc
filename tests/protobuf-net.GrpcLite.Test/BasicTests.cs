using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    public async ValueTask CanCreateInvoker()
    {
        await using var channel = new LiteChannel(new StreamFrameConnection(Stream.Null, Stream.Null), Me());
        var invoker = channel.CreateCallInvoker();
    }

    class DummyHandler : HandlerBase<string, string>, IMethod
    {
        public DummyHandler(string fullName, ushort streamId)
        {
            _fullName = fullName;
            Method = this;
            Initialize(streamId, null!, null);
        }

        private readonly string _fullName;

        protected override bool IsClient => true;

        protected override Action<string, SerializationContext> Serializer => throw new NotImplementedException();
        protected override Func<DeserializationContext, string> Deserializer => throw new NotImplementedException();

        MethodType IMethod.Type => MethodType.Unary;

        string IMethod.ServiceName => throw new NotImplementedException();

        string IMethod.Name => throw new NotImplementedException();

        string IMethod.FullName => _fullName;

        public override ValueTask CompleteAsync(CancellationToken cancellationToken) => default;

        public override void Recycle() { }

        protected override ValueTask ReceivePayloadAsync(string value, CancellationToken cancellationToken) => default;
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
        var handler = new DummyHandler("/myservice/mymethod", 42);
        var frame = handler.GetInitializeFrame("");

        var hex = GetHex(frame.Buffer);
        Assert.Equal(
    "01-00-2A-00-00-00-13-00-" // unary, id 42, length 19
    + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64", hex); // "/myservice/mymethod"

        frame = new Frame(frame.Buffer);
        var header = frame.GetHeader();
        Assert.Equal(FrameKind.NewStream, header.Kind);
        Assert.Equal(0, header.KindFlags);
        Assert.Equal(42, header.StreamId);
        Assert.Equal(0, header.SequenceId);
        Assert.Equal(19, header.PayloadLength);
        Assert.Equal("/myservice/mymethod", Encoding.UTF8.GetString(frame.GetPayload().Span));

        frame.Release();

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
        await using var channel = new LiteChannel(new StreamFrameConnection(Stream.Null, ms), Me());
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
            + "02-03-00-00-01-00-0D-00-" // payload, final chunk, final element, id 0/1, length 13
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21", hex); // "hello, world!"
    }

    [Fact]
    public async Task CanWriteSimpleSyncMessage()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var ms = new MemoryStream();
        await using var channel = new LiteChannel(new StreamFrameConnection(Stream.Null, ms), Me());
        var invoker = channel.CreateCallInvoker();

        // we don't expect a reply
        var oce = Assert.Throws<TaskCanceledException>(() => invoker.BlockingUnaryCall(new Method<string, string>(MethodType.Unary, "myservice", "mymethod", StringMarshaller_Simple, StringMarshaller_Simple), "",
            default(CallOptions).WithCancellationToken(cts.Token), "hello, world!"));
        Assert.Equal(cts.Token, oce.CancellationToken);

        var hex = GetHex(ms);
        Assert.Equal(
            "01-00-00-00-00-00-13-00-" // unary, id 0/0, length 19
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "02-03-00-00-01-00-0D-00-" // payload, final chunk, final element, id 0/1 length 13
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21", hex); // "hello, world!"
    }

    [Fact]
    public void CanBindService()
    {
        var server = new TestStreamServer(Logger);
        server.AddConnection(new StreamFrameConnection(Stream.Null, Stream.Null), CancellationToken.None);
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

