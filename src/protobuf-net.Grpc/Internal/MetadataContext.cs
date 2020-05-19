using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Grpc.Core;

namespace ProtoBuf.Grpc.Internal
{
    internal sealed class MetadataContext
    {
        internal MetadataContext(object? state) => State = state;

        internal object? State { get; }
        private Metadata? _headers, _trailers;
        internal Metadata Headers
        {
            get => _headers ?? Throw("Headers are not yet available");
        }
        internal Metadata Trailers
        {
            get => _trailers ?? Throw("Trailers are not yet available");
        }
        internal Status Status { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Metadata Throw(string message) => throw new InvalidOperationException(message);

        internal MetadataContext Reset()
        {
            Status = Status.DefaultSuccess;
            _headers = _trailers = null;
            return this;
        }

        internal void SetTrailers(RpcException fault)
        {
            if (fault is object)
            {
                _trailers = fault.Trailers ?? Metadata.Empty;
                Status = fault.Status;
            }
        }

        internal void SetTrailers<T>(T call, Func<T, Status> getStatus, Func<T, Metadata> getMetadata)
            where T : class
        {
            try
            {
                _trailers = getMetadata(call) ?? Metadata.Empty;
                Status = getStatus(call);
            }
            catch (RpcException fault)
            {
                SetTrailers(fault);
                throw;
            }
        }

        internal ValueTask SetHeadersAsync(Task<Metadata> headers)
        {
            if (headers.RanToCompletion())
            {
                _headers = headers.Result;
                return default;
            }
            else
            {
                return Awaited(this, headers);
            }
            static async ValueTask Awaited(MetadataContext context, Task<Metadata> headers)
            {
                try
                {
                    context._headers = await headers.ConfigureAwait(false);
                }
                catch (RpcException fault)
                {
                    context.SetTrailers(fault);
                    throw;
                }
            }
        }
    }
}
