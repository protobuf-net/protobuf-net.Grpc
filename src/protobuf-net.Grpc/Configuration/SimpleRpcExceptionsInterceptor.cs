using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.IO;
using System.Security;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// A base interceptor that handles all server-side exceptions.
    /// </summary>
    public abstract class ServerExceptionsInterceptorBase : Interceptor
    {
        /// <summary>
        /// Allows implementors to intercept exceptions, optionally re-exposing them as <see cref="RpcException"/>.
        /// </summary>
        /// <returns><c>true</c> if the exception should be re-exposed as an <see cref="RpcException"/>, <c>false</c> otherwise</returns>
        protected virtual bool OnException(Exception exception, out Status status)
        {
            status = default;
            return false;
        }

        /// <inheritdoc/>
        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await base.ClientStreamingServerHandler(requestStream, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (OnException(ex, out var status))
            {
                throw new RpcException(status, ex.Message);
            }
        }

        /// <inheritdoc/>
        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                await base.ServerStreamingServerHandler(request, responseStream, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (OnException(ex, out var status))
            {
                throw new RpcException(status, ex.Message);
            }
        }

        /// <inheritdoc/>
        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                await base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (OnException(ex, out var status))
            {
                throw new RpcException(status, ex.Message);
            }
        }

        /// <inheritdoc/>
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await base.UnaryServerHandler(request, context, continuation).ConfigureAwait(false);
            }
            catch (Exception ex) when (OnException(ex, out var status))
            {
                throw new RpcException(status, ex.Message);
            }
        }
    }

    /// <summary>
    /// Indicates that a service or method should use simplified exception handling - which means that all server exceptions are treated as <see cref="RpcException"/>; this
    /// will expose the <see cref="Exception.Message"/> to the caller (and the type may be interpreted as a <see cref="StatusCode"/> when possible), which should only be
    /// done with caution as this may present security implications. Additional exception metadata (<see cref="Exception.Data"/>, <see cref="Exception.InnerException"/>,
    /// <see cref="Exception.StackTrace"/>, etc) is not propagated. The exception is still exposed at the client as an <see cref="RpcException"/>.
    /// </summary>
    public class SimpleRpcExceptionsInterceptor : ServerExceptionsInterceptorBase
    {
        private SimpleRpcExceptionsInterceptor() { }
        private static SimpleRpcExceptionsInterceptor? s_Instance;

        /// <summary>
        /// Provides a shared instance of this interceptor
        /// </summary>
        public static SimpleRpcExceptionsInterceptor Instance => s_Instance ??= new SimpleRpcExceptionsInterceptor();

        internal static bool ShouldWrap(Exception exception, out Status status)
        {
            if (exception is RpcException)
            {
                status = default;
                return false;
            }
            status = new Status(exception switch
            {
#pragma warning disable IDE0059 // needs more recent compiler than the CI server has
                OperationCanceledException a => StatusCode.Cancelled,
                ArgumentException b => StatusCode.InvalidArgument,
                NotImplementedException c => StatusCode.Unimplemented,
                NotSupportedException d => StatusCode.Unimplemented,
                SecurityException e => StatusCode.PermissionDenied,
                EndOfStreamException f => StatusCode.OutOfRange,
                FileNotFoundException g => StatusCode.NotFound,
                DirectoryNotFoundException h => StatusCode.NotFound,
                TimeoutException i => StatusCode.DeadlineExceeded,
#pragma warning restore IDE0059 // needs more recent compiler than the CI server has
                _ => StatusCode.Unknown,
            }, exception.Message);
            return true;
        }

        /// <inheritdoc/>
        protected override bool OnException(Exception exception, out Status status)
            => ShouldWrap(exception, out status);
    }
}
