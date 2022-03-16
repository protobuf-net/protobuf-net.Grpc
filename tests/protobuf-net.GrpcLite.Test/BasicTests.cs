using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
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

    class DummyHandler : LiteStream<string, string>
    {
        class SimpleMethod : IMethod
        {
            public SimpleMethod(string fullName) => FullName = fullName;
            MethodType IMethod.Type => MethodType.Unary;

            string IMethod.ServiceName => throw new NotImplementedException();

            string IMethod.Name => throw new NotImplementedException();

            public string FullName { get; }
        }
        public DummyHandler(string fullName, ushort id)
            : base(new SimpleMethod(fullName), null!)
        {
            Id = id;
        }

        protected override bool IsClient => true;

        protected override Action<string, SerializationContext> Serializer => throw new NotImplementedException();
        protected override Func<DeserializationContext, string> Deserializer => throw new NotImplementedException();


        protected override ValueTask ExecuteAsync() => throw new NotImplementedException();
        protected override ValueTask OnPayloadAsync(string value) => default;
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

        var hex = GetHex(frame.RawBuffer);
        Assert.Equal(
    "02-00-2A-00-00-00-13-00-" // unary, id 42, length 19
    + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64", hex); // "/myservice/mymethod"

        frame = new Frame(frame.RawBuffer);
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
        var oce = await Assert.ThrowsAsync<TaskCanceledException>(() => call.ResponseAsync);
        Assert.Equal(cts.Token, oce.CancellationToken);
        await Task.Delay(100);
        pipe.Close();
        await pipe.Complete;
        var hex = await GetHexAsync(server);
        Assert.Equal(
            "02-00-00-00-00-00-13-00-" // unary, id 0/0, length 19
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "04-03-00-00-01-00-0D-00-" // payload, final chunk, final element, id 0/1, length 13
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21", hex); // "hello, world!"
    }

    private async ValueTask<string> GetHexAsync(IDuplexPipe pipe)
    {
        using var ms = new MemoryStream();
        await pipe.Input.CopyToAsync(ms);
        return GetHex(ms);
    }

    private CancellationTokenSource Timeout(int seconds = 2)
        => Debugger.IsAttached ? new CancellationTokenSource() : new CancellationTokenSource(seconds);

    [Fact]
    public async Task CanWriteSimpleSyncMessage()
    {
        var pipe = CreateClientPipe(out var server);
        using var cts = Timeout();
        await using var channel = new LiteChannel(pipe, Me(), Logger);
        var invoker = channel.CreateCallInvoker();

        // we don't expect a reply
        var oce = Assert.Throws<TaskCanceledException>(() => invoker.BlockingUnaryCall(new Method<string, string>(MethodType.Unary, "myservice", "mymethod", StringMarshaller_Simple, StringMarshaller_Simple), "",
            default(CallOptions).WithCancellationToken(cts.Token), "hello, world!"));
        Assert.Equal(cts.Token, oce.CancellationToken);
        await Task.Delay(100);
        pipe.Close();
        await pipe.Complete;

        var hex = await GetHexAsync(server);
        Assert.Equal(
            "02-00-00-00-00-00-13-00-" // unary, id 0/0, length 19
            + "2F-6D-79-73-65-72-76-69-63-65-2F-6D-79-6D-65-74-68-6F-64-" // "/myservice/mymethod"
            + "04-03-00-00-01-00-0D-00-" // payload, final chunk, final element, id 0/1 length 13
            + "68-65-6C-6C-6F-2C-20-77-6F-72-6C-64-21", hex); // "hello, world!"
    }

    [Fact]
    public void CanBindService()
    {
        var server = new LiteServer(Logger);
        server.ManualBind<MyService>();
        Assert.Equal(2, server.MethodCount);
    }

    [Fact]
    public void CanCreateTestFixture()
    {
        using var obj = new TestServerHost();
    }



}

