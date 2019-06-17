using Grpc.Core;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal
{
    [Obsolete(Reshape.WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Empty : IEquatable<Empty>
    {
        public static readonly Empty Instance = new Empty();
        internal static readonly Task<Empty> InstanceTask = Task.FromResult(Instance);
        private Empty() { }
        public override string ToString() => nameof(Empty);
        public override bool Equals(object? obj) => obj is Empty;
        public override int GetHashCode() => 42;
        bool IEquatable<Empty>.Equals(Empty other) => other != null;

        internal static readonly Marshaller<Empty> Marshaller
            = new Marshaller<Empty>((Empty _)=> Array.Empty<byte>(), (byte[] _) => Instance);
    }
}
