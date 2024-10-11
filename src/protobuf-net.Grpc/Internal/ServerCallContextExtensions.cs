using Grpc.Core;
using System;
using System.IO;

namespace ProtoBuf.Grpc.Experimental
{
    /// <summary>
    /// Utility methods for <see cref="Stream"/>.
    /// </summary>
    internal static class ServerCallContextExtensions
    {
        private static readonly object TrailerCallback = new();
        /// <summary>
        /// Allow a <see cref="Stream"> to provide trailer data after completion.
        /// </summary>
        public static ServerCallContext WithTrailers(this ServerCallContext context, Action<Metadata> callback)
        {
            context.UserState[TrailerCallback] = callback;
            return context;
        }

        internal static void ApplyTrailers(this ServerCallContext context)
        {
            if (context.UserState.TryGetValue(TrailerCallback, out var value)
                && value is Action<Metadata> callback)
            {
                callback(context.ResponseTrailers);
            }
        }
    }
}