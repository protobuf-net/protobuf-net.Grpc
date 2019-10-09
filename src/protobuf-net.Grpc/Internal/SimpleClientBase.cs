using Grpc.Core;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Internal
{
    /// <summary>
    /// A basic CallInvoker-based client
    /// </summary>
    [Obsolete(Reshape.WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SimpleClientBase // like LiteClientBase from API when we can
    {
        /// <summary>
        /// Gets the call-invoker associated with this instance
        /// </summary>
        protected CallInvoker CallInvoker
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Create a new instance  
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected SimpleClientBase(CallInvoker callInvoker) => CallInvoker = callInvoker;
    }
}