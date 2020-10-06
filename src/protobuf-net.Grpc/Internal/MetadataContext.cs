using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace ProtoBuf.Grpc.Internal
{
    internal sealed class MetadataContext
    {
        internal MetadataContext(object? state) => State = state;

        internal object? State { get; }
        private Metadata? _trailers;
        private object? _headersTaskOrSource;

        internal Metadata Headers
        {
            get
            {
                var pending = GetHeadersTask(false);
                return pending is object && pending.RanToCompletion()
                    ? pending.Result
                    : Throw("Headers are not yet available");
            }
        }

        internal Task<Metadata>? GetHeadersTask(bool createIfMissing)
        {
            return _headersTaskOrSource switch
            {
                Task<Metadata> task => task,
                TaskCompletionSource<Metadata> tcs => tcs.Task,
                _ => createIfMissing ? InterlockedCreateSource() : null,
            };

            Task<Metadata> InterlockedCreateSource()
            {
                var newTcs = new TaskCompletionSource<Metadata>();
                var existing = Interlocked.CompareExchange(ref _headersTaskOrSource, newTcs, null);
                return existing switch
                {
                    Task<Metadata> task => task,
                    TaskCompletionSource<Metadata> tcs => tcs.Task,
                    _ => newTcs.Task,
                };
            }
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
            _trailers = null;
            _headersTaskOrSource = null;
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
            var tcs = Interlocked.CompareExchange(ref _headersTaskOrSource, headers, null) as TaskCompletionSource<Metadata>;
            if (headers.RanToCompletion())
            {
                // headers are sync; update TCS if one
                tcs?.TrySetResult(headers.Result);
                return default;
            }
            else
            {
                // headers are async (or faulted); pay the piper
                return Awaited(this, tcs, headers);
            }

            static async ValueTask Awaited(MetadataContext context, TaskCompletionSource<Metadata>? tcs, Task<Metadata> headers)
            {
                try
                {
                    tcs?.TrySetResult(await headers.ConfigureAwait(false));
                }
                catch (RpcException fault)
                {
                    context.SetTrailers(fault);
                    tcs?.TrySetException(fault);
                    throw;
                }
                catch (Exception ex)
                {
                    tcs?.TrySetException(ex);
                    throw;
                }
            }
        }
    }
}
