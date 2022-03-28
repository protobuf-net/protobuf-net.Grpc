using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal interface IClientStream : IStream, IDisposable
{
    Task<Metadata> ResponseHeadersAsync { get; }

    Status Status { get; }
    Metadata Trailers();
}

internal sealed class ClientStream<TRequest, TResponse> : LiteStream<TRequest, TResponse>, IClientStreamWriter<TRequest>, IClientStream where TRequest : class where TResponse : class
{
    public override void Dispose()
    {
        base.Dispose();
        var tmp = _ctr;
        _ctr = default;
        tmp.SafeDispose();
    }

    private CancellationTokenRegistration _ctr;
    internal override CancellationTokenRegistration RegisterForCancellation(CancellationToken streamSpecificCancellation, DateTime? deadline)
        => _ctr = base.RegisterForCancellation(streamSpecificCancellation, deadline);

    public ClientStream(IMethod method, IConnection owner, ILogger? logger)
        : base(method, owner)
    {
        Logger = logger;
    }
    protected sealed override bool IsClient => true;

    Task IClientStreamWriter<TRequest>.CompleteAsync()
        => SendTrailerAsync(null, null, FrameWriteFlags.None).AsTask();

    Task IAsyncStreamWriter<TRequest>.WriteAsync(TRequest message) => SendAsync(message, WriterFlags).AsTask();

    protected override void OnCancel()
    {
        _status = new Status(StatusCode.Cancelled, "");
        var old = Interlocked.CompareExchange(ref _headers, CanceledSentinel, null);
        if (old is TaskCompletionSource<Metadata> pending)
            pending.TrySetCanceled();
        TrySendCancellation();
    }

    public Status Status
    {
        get
        {
            var status = _status.GetValueOrDefault();
            if (_status.HasValue) Throw();
            return status;
            static void Throw() => throw new InvalidOperationException("The status is not yet available; you should await the full response");
        }
    }

    private Status? _status;
    protected override void OnTrailers()
    {
        var status = MetadataEncoder.GetStatus(RawTrailers);
        _status = status;
        if (status.StatusCode != StatusCode.OK)
        {
            try
            {
                ThrowStatus(status);
            }
            catch (Exception ex)
            {
                ReportFault(ex);
                throw;
            }
        }
    }

    protected override void ReportFault(Exception ex, [CallerMemberName] string caller = "")
    {
        base.ReportFault(ex, caller);
        var pending = Interlocked.CompareExchange(ref _headers, CanceledSentinel, null) as TaskCompletionSource<Metadata>;
        pending?.TrySetException(ex);
    }

    private static readonly object CompletedSentinel = new object(), CanceledSentinel = new object();
    protected override void OnHeaders()
    {
        var old = Interlocked.CompareExchange(ref _headers, CompletedSentinel, null);
        if (old is TaskCompletionSource<Metadata> pending)
            pending.SetResult(MetadataEncoder.GetMetadata(RawHeaders, Connection));
    }

    private void ThrowStatus(Status status)
        => throw new RpcException(status, Trailers());

    protected override Action<TRequest, SerializationContext> Serializer => TypedMethod.RequestMarshaller.ContextualSerializer;
    protected override Func<DeserializationContext, TResponse> Deserializer => TypedMethod.ResponseMarshaller.ContextualDeserializer;
    private Method<TRequest, TResponse> TypedMethod => Unsafe.As<Method<TRequest, TResponse>>(Method);


    private Metadata? _trailers;
    public Metadata Trailers()
    {
        if (!_status.HasValue) Throw();
        static void Throw() => throw new InvalidOperationException("The trailers are not yet available; you should await the full response");
        return _trailers ??= MetadataEncoder.GetMetadata(RawTrailers, Connection);
    }

    private object? _headers;
    public Task<Metadata> ResponseHeadersAsync
    {
        get
        {
            while (true)
            {
                var old = Volatile.Read(ref _headers);
                if (old is TaskCompletionSource<Metadata> pending) return pending.Task;

                if (old is Task<Metadata> completed) return completed;

                if (ReferenceEquals(old, CompletedSentinel))
                {   // create a completed task with that result
                    completed = Task.FromResult(MetadataEncoder.GetMetadata(RawHeaders, Connection));
                    Interlocked.Exchange(ref _headers, completed); // avoid multiple Metadata instances
                    return completed;
                }
                if (ReferenceEquals(old, CanceledSentinel))
                {   // create a completed task with that result
                    completed = Task.FromCanceled<Metadata>(default);
                    Interlocked.Exchange(ref _headers, completed);
                    return completed;
                }

                Debug.Assert(old is null);
                // create a new pending result
                var tcs = new TaskCompletionSource<Metadata>();
                if (ReferenceEquals(Interlocked.CompareExchange(ref _headers, tcs, old), old))
                    return tcs.Task;

                // otherwise, redo from start
            }
        }
    }
}
