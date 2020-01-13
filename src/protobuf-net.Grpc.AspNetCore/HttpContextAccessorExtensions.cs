using System;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using ProtoBuf.Grpc;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// Provides access to http-context specific extension features
    /// </summary>
    public static class HttpContextAccessorExtensions
    {
        /// <summary>
        /// Gets the server-call-context associated with the current http context, if possible
        /// </summary>
        public static ServerCallContext GetServerCallContext(this IHttpContextAccessor httpContextAccessor)
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("Unable to propagate server context values to the call. Can't find the current HttpContext.");
            }

            var serverCallContext = httpContext.Features.Get<IServerCallContextFeature>()?.ServerCallContext;
            if (serverCallContext == null)
            {
                throw new InvalidOperationException("Unable to propagate server context values to the call. Can't find the current gRPC ServerCallContext.");
            }

            return serverCallContext;
        }

        /// <summary>
        /// Gets the call-context associated with the current http context, if possible
        /// </summary>
        public static CallContext GetCallContext(this IHttpContextAccessor httpContextAccessor, object service)
        {
            var serverCallContext = GetServerCallContext(httpContextAccessor);
            return new CallContext(service, serverCallContext);
        }
    }
}