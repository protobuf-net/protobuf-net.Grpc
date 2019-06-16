using System;
using System.Runtime.CompilerServices;
using Grpc.Core;

namespace ProtoBuf.Grpc.Internal
{
    internal sealed class MetadataContext
    {
        internal MetadataContext() { }

        private Metadata? _headers, _trailers;
        internal Metadata Headers
        {
            get => _headers ?? Throw("Headers are not yet available");
            set => _headers = value;
        }
        internal Metadata Trailers
        {
            get => _trailers ?? Throw("Trailers are not yet available");
            set => _trailers = value;
        }
        internal Status Status { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Metadata Throw(string message) => throw new InvalidOperationException(message);

        internal MetadataContext Reset()
        {
            Status = default;
            _headers = _trailers = null;
            return this;
        }
    }
}
