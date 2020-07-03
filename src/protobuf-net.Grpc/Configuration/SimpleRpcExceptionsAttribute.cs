using Grpc.Core;
using System;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Indicates that a service or method should use simplified exception handling - which means that all server exceptions are treated as <see cref="RpcException"/>; this
    /// will expose the <see cref="Exception.Message"/> to the caller (and the type may be interpreted as a <see cref="StatusCode"/> when possible), which should only be
    /// done with caution as this may present security implications. Additional exception metadata (<see cref="Exception.Data"/>, <see cref="Exception.InnerException"/>,
    /// <see cref="Exception.StackTrace"/>, etc) is not propagated. The exception is still exposed at the client as an <see cref="RpcException"/>.
    /// </summary>
    /// <remarks>This feature is only currently supported on <c>async</c> methods that expose their faults via the returned awaitable, not by throwing directly; a more robust
    /// implementation is provided by the <see cref="SimpleRpcExceptionsInterceptor"/> interceptor.</remarks>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class SimpleRpcExceptionsAttribute : Attribute
    {
        /// <summary>
        /// Gets a shared instance of this attribute type
        /// </summary>
        public static SimpleRpcExceptionsAttribute Default => s_Default ??= new SimpleRpcExceptionsAttribute();

        private static SimpleRpcExceptionsAttribute? s_Default;
    }
}
