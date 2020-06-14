using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Defines a non-buffered push API that represents async enumerable data
    /// </summary>
    public interface IPushAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        /// <summary>
        /// Indicates whether this sequence is completed
        /// </summary>
        bool IsCompleted { get; }
        /// <summary>
        /// Indicates the end of the sequence
        /// </summary>
        void Complete(Exception? error = null);
        /// <summary>
        /// Sends an element to the sequence and awaits its consumption
        /// </summary>
        ValueTask PushAsync(T value);
    }

    /// <summary>
    /// Allows construction of <see cref="IPushAsyncEnumerable{T}"/> instances
    /// </summary>
    public static class PushAsyncEnumerable
    {
        /// <summary>
        /// Create a new <see cref="IPushAsyncEnumerable{T}"/> instance
        /// </summary>
        public static IPushAsyncEnumerable<T> Create<T>() => new PushAsyncEnumerableCore<T>();

        static readonly Exception
            s_CanceledSentinel = new OperationCanceledException(),
            s_CompletedSentinel = new InvalidOperationException();

        private static readonly Action<object> s_CancelCallback = s => (s as ICancellableInner)?.Cancel();

        private interface ICancellableInner
        {
            void Cancel();
        }
        private sealed class PushAsyncEnumerableCore<T> : ICancellableInner,
            IPushAsyncEnumerable<T>, IAsyncEnumerator<T>,
            IValueTaskSource<bool>, IValueTaskSource
        {
            ManualResetValueTaskSourceCore<bool> _moveNext;
            ManualResetValueTaskSourceCore<int> _consumed;
            CancellationTokenRegistration _cancellationTokenRegistration;

            public bool IsCompleted { get; private set; }

            public PushAsyncEnumerableCore()
            {
                _current = _next = default!;
                _moveNext.RunContinuationsAsynchronously = false;
            }

            private T _current, _next;
            T IAsyncEnumerator<T>.Current => _current;

            void ICancellableInner.Cancel() => Complete(s_CanceledSentinel);
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                Complete(s_CompletedSentinel);
                return default;
            }

            public void Complete(Exception? error = null)
            {
                if (IsCompleted) return;

                // unregister from cancellation
                var tmp = _cancellationTokenRegistration;
                _cancellationTokenRegistration = default;
                try
                {
                    tmp.Dispose();
                }
                catch { }

                // set outcomes
                if (error is null)
                {
                    _moveNext.SetResult(false);
                    TrySetException(ref _consumed, s_CompletedSentinel);
                }
                else
                {
                    TrySetException(ref _moveNext, error);
                    TrySetException(ref _consumed, error);
                }
                IsCompleted = true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void TrySetException<TCore>(ref ManualResetValueTaskSourceCore<TCore> source, Exception exception)
            {
                try
                {
                    source.SetException(exception);
                }
                catch { }
            }


            public ValueTask PushAsync(T value)
            {
                if (!IsCompleted) // in any scenario where we are completed, we've already set a fault on the VT
                {
                    _next = value;
                    _moveNext.SetResult(true);
                }
                return new ValueTask(this, _consumed.Version);
            }



            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void HideSentinels(Exception ex)
            {
                if (ReferenceEquals(ex, s_CanceledSentinel)) ThrowCancelled();
                if (ReferenceEquals(ex, s_CompletedSentinel)) ThrowCompleted();

                [MethodImpl(MethodImplOptions.NoInlining)]
                static void ThrowCancelled() => throw new OperationCanceledException();
                [MethodImpl(MethodImplOptions.NoInlining)]
                static void ThrowCompleted() => throw new InvalidOperationException("Cannot push to a sequence that has been completed");
            }

            IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _cancellationTokenRegistration = cancellationToken.Register(s_CancelCallback, this, false);
                }
                return this;
            }

            ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
                => new ValueTask<bool>(this, _moveNext.Version);

            bool IValueTaskSource<bool>.GetResult(short token)
            {
                try
                {
                    var result = _moveNext.GetResult(token);
                    if (result)
                    {
                        _current = _next;
                        _next = default!;
                        try { _consumed.SetResult(0); }
                        catch { }
                    }
                    else
                    {
                        _current = _next = default!;
                    }
                    _moveNext.Reset();
                    return result;
                }
                catch (Exception ex)
                {
                    HideSentinels(ex);
                    throw;
                }
            }

            ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _moveNext.GetStatus(token);

            void IValueTaskSource<bool>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
                => _moveNext.OnCompleted(continuation, state, token, flags);

            void IValueTaskSource.GetResult(short token)
            {
                try
                {
                    _consumed.GetResult(token);
                    _consumed.Reset();
                }
                catch (Exception ex)
                {
                    HideSentinels(ex);
                    throw;
                }
            }

            ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _consumed.GetStatus(token);

            void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
                => _consumed.OnCompleted(continuation, state, token, flags);
        }
    }
}
