using Grpc.Core;
using System;
using System.Globalization;
using System.Threading;

namespace ProtoBuf.Grpc.Lite.Internal;

internal class MultiChannel : ChannelBase
{
    private readonly CallInvoker _invoker;
    public MultiChannel(ChannelBase[] channels, string? target = null) : base(AutoName(channels, target))
        => _invoker = new MultiInvoker(channels);

    private static string AutoName(ChannelBase[] channels, string? target) =>
        string.IsNullOrWhiteSpace(target) ? (nameof(MultiChannel) + ":" + channels.Length.ToString(CultureInfo.InvariantCulture))
            : target!.Trim();
    public override CallInvoker CreateCallInvoker() => _invoker;

    private class MultiInvoker : CallInvoker
    {
        private int _index;
        private readonly CallInvoker[] _callInvokers;

        public MultiInvoker(ChannelBase[] channels)
            => _callInvokers = Array.ConvertAll(channels, static c => c.CreateCallInvoker());

        public CallInvoker GetNext() => _callInvokers[(uint)Interlocked.Increment(ref _index) % _callInvokers.Length];

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            => GetNext().BlockingUnaryCall(method, host, options, request);

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            => GetNext().AsyncUnaryCall(method, host, options, request);

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            => GetNext().AsyncServerStreamingCall(method, host, options, request);

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            => GetNext().AsyncClientStreamingCall(method, host, options);

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            => GetNext().AsyncDuplexStreamingCall(method, host, options);
    }
}
