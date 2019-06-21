using Grpc.Core;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal
{
    /// <summary>
    /// Represents a void request or result
    /// </summary>
    [Obsolete(Reshape.WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Empty : IEquatable<Empty>
    {
        /// <summary>
        /// The singleton instance of this type
        /// </summary>
        public static readonly Empty Instance = new Empty();
        /// <summary>
        /// The singleton instance of this type, as a task
        /// </summary>
        public static readonly Task<Empty> InstanceTask = Task.FromResult(Instance);
        private Empty() { }
        /// <summary>
        /// Represents the value as a string
        /// </summary>
        public override string ToString() => nameof(Empty);
        /// <summary>
        /// Compares two instances for equality
        /// </summary>
        public override bool Equals(object? obj) => obj is Empty;
        /// <summary>
        /// Compares two instances for equality
        /// </summary>
        public override int GetHashCode() => 42;
        bool IEquatable<Empty>.Equals(Empty other) => other != null;

        internal static readonly Marshaller<Empty> Marshaller
            = new Marshaller<Empty>((Empty _)=> Array.Empty<byte>(), (byte[] _) => Instance);
    }
}
