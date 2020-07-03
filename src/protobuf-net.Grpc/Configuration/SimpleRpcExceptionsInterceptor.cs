using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Indicates that a service or method should use simplified exception handling - which means that all server exceptions are treated as <see cref="RpcException"/>; this
    /// will expose the <see cref="Exception.Message"/> to the caller (and the type may be interpreted as a <see cref="StatusCode"/> when possible), which should only be
    /// done with caution as this may present security implications. Additional exception metadata (<see cref="Exception.Data"/>, <see cref="Exception.InnerException"/>,
    /// <see cref="Exception.StackTrace"/>, etc) is not propagated. The exception is still exposed at the client as an <see cref="RpcException"/>.
    /// </summary>
    public sealed class SimpleRpcExceptionsInterceptor : Interceptor
    {
        private SimpleRpcExceptionsInterceptor() { }
        private static SimpleRpcExceptionsInterceptor? s_Instance;
        /// <summary>
        /// Provides a shared instance of this interceptor
        /// </summary>
        public static SimpleRpcExceptionsInterceptor Instance => s_Instance ??= new SimpleRpcExceptionsInterceptor();

        /// <inheritdoc/>
        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await base.ClientStreamingServerHandler(requestStream, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNotRpcException(ex))
            {
                RethrowAsRpcException(ex);
                return default!; // make compiler happy
            }
        }

        /// <inheritdoc/>
        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                await base.ServerStreamingServerHandler(request, responseStream, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNotRpcException(ex))
            {
                RethrowAsRpcException(ex);
            }
        }

        /// <inheritdoc/>
        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                await base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNotRpcException(ex))
            {
                RethrowAsRpcException(ex);
            }
        }

        /// <inheritdoc/>
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await base.UnaryServerHandler(request, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNotRpcException(ex))
            {
                RethrowAsRpcException(ex);
                return default!; // make compiler happy
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsNotRpcException(Exception ex) => !(ex is RpcException);

        internal static void RethrowAsRpcException(Exception ex)
        {
            var code = ex switch
            {
                OperationCanceledException => StatusCode.Cancelled,
                ArgumentException => StatusCode.InvalidArgument,
                NotImplementedException => StatusCode.Unimplemented,
                SecurityException => StatusCode.PermissionDenied,
                EndOfStreamException => StatusCode.OutOfRange,
                FileNotFoundException => StatusCode.NotFound,
                DirectoryNotFoundException => StatusCode.NotFound,
                TimeoutException => StatusCode.DeadlineExceeded,
                _ => StatusCode.Unknown,
            };
            throw new RpcException(new Status(code, ex.Message), ex.Message);
        }
    }
}
