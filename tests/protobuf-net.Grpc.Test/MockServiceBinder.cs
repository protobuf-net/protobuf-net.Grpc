using System;
using System.Collections.Generic;
using Grpc.Core;

namespace protobuf_net.Grpc.Test
{
    public class MockServiceBinder : ServiceBinderBase
    {
        public Dictionary<string, Delegate> Handlers { get; } = new Dictionary<string, Delegate>();

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            UnaryServerMethod<TRequest, TResponse> handler)
        {
            Handlers.Add(method.FullName, handler);
        }

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            ClientStreamingServerMethod<TRequest, TResponse> handler)
        {
            Handlers.Add(method.FullName, handler);
        }

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            ServerStreamingServerMethod<TRequest, TResponse> handler)
        {
            Handlers.Add(method.FullName, handler);
        }

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            DuplexStreamingServerMethod<TRequest, TResponse> handler)
        {
            Handlers.Add(method.FullName, handler);
        }
    }
}
