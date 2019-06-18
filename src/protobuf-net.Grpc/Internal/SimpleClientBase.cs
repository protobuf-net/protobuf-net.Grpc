using Grpc.Core;
using System;
using System.ComponentModel;

namespace ProtoBuf.Grpc.Internal
{
    [Obsolete(Reshape.WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class SimpleClientBase // replace with LiteClientBase from API when we can
    {
        protected CallInvoker CallInvoker { get; }
        public SimpleClientBase(CallInvoker callInvoker) => CallInvoker = callInvoker;
    }
}
