using Grpc.Core;
using System;
using System.ComponentModel;

namespace ProtoBuf.Grpc.Internal
{
    /// <summary>
    /// A basic CallInvoker-based client
    /// </summary>
    [Obsolete(Reshape.WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SimpleClientBase // replace with LiteClientBase from API when we can
    {
        /// <summary>
        /// Gets the call-invoker associated with this instance
        /// </summary>
        protected CallInvoker CallInvoker { get; }

        /// <summary>
        /// Create a new instance  
        /// </summary>
        protected SimpleClientBase(CallInvoker callInvoker) => CallInvoker = callInvoker;
    }
}
