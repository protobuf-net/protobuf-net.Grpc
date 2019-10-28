using System;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using ProtoBuf.Grpc;

namespace Microsoft.AspNetCore.Http
{
    public static class HttpContextAccessorExtensions
    {
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

        public static CallContext GetCallContext(this IHttpContextAccessor httpContextAccessor, object service)
        {
            var serverCallContext = GetServerCallContext(httpContextAccessor);
            return new CallContext(service, serverCallContext);
        }
    }
}