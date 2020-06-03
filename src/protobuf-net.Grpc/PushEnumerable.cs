using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        ValueTask PushAsync(T value, CancellationToken cancellationToken = default);
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
            s_DisposedSentinel = new ObjectDisposedException(nameof(PushAsyncEnumerable)),
            s_CanceledSentinel = new TaskCanceledException();

        private static readonly Action<object> s_CancelCallback = s => (s as ICancellableInner)?.Cancel();

        private interface ICancellableInner
        {
            void Cancel();
        }
        private sealed class PushAsyncEnumerableCore<T> : ICancellableInner,
            IPushAsyncEnumerable<T>, IAsyncEnumerator<T>,
            IValueTaskSource<bool>, IValueTaskSource<int>
        {
            ManualResetValueTaskSourceCore<bool> _moveNext;
            ManualResetValueTaskSourceCore<int> _consumed;
            CancellationTokenRegistration _cancellationTokenRegistration;
            private bool _isCompleted;
            public bool IsCompleted => _isCompleted;

            private void SetCompleted([CallerMemberName] string caller = "")
            {
                if (!_isCompleted)
                {
                    Debug.WriteLine($"{nameof(PushAsyncEnumerable)}<{typeof(T).Name}> completed by {caller}");
                }
                _isCompleted = true;
            }
            public PushAsyncEnumerableCore()
            {
                _current = default!;
                _moveNext.RunContinuationsAsynchronously = false;
            }

            private T _current;
            T IAsyncEnumerator<T>.Current => _current;

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                SetCompleted();
                try
                {
                    _moveNext.SetException(s_DisposedSentinel);
                }
                catch { }
                try
                {
                    _consumed.SetResult(0);
                }
                catch { }
                var tmp = _cancellationTokenRegistration;
                _cancellationTokenRegistration = default;
                try
                {
                    tmp.Dispose();
                }
                catch { }
                return default;
            }

            public void Complete(Exception? error = null)
            {
                SetCompleted();
                if (error is null)
                {
                    _moveNext.SetResult(false);
                    _consumed.SetResult(0);
                }
                else
                {
                    try { _moveNext.SetException(error); }
                    catch { }
                    try { _consumed.SetException(error); }
                    catch { }
                }
            }

            void ICancellableInner.Cancel()
            {
                SetCompleted();
                try
                {
                    _moveNext.SetException(s_CanceledSentinel);
                }
                catch { }
                try
                {
                    _consumed.SetException(s_CanceledSentinel);
                }
                catch { }
            }

            public ValueTask PushAsync(T value, CancellationToken cancellationToken = default)
            {
                _consumed.Reset();
                var consumed = new ValueTask<int>(this, _consumed.Version);
                _current = value;
                _moveNext.SetResult(true);
                return consumed.IsCompletedSuccessfully ? default
                    : AwaitConsumed(consumed, cancellationToken);
            }

            private async ValueTask AwaitConsumed(ValueTask<int> consumed, CancellationToken cancellationToken)
            {
                using (cancellationToken.Register(s_CancelCallback, this, false))
                {
                    try
                    {
                        await consumed.ConfigureAwait(false);
                    }
                    catch(ObjectDisposedException ode) when (ReferenceEquals(ode, s_DisposedSentinel))
                    {
                        ThrowDisposed();
                    }
                }
            }

            private void ThrowDisposed()
                => throw new ObjectDisposedException(nameof(PushAsyncEnumerable));

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
            {
                _consumed.SetResult(0);
                _moveNext.Reset();
                return new ValueTask<bool>(this, _moveNext.Version);
            }

            bool IValueTaskSource<bool>.GetResult(short token)
            {
                try
                {
                    return _moveNext.GetResult(token);
                }
                catch (TaskCanceledException tce) when (ReferenceEquals(tce, s_CanceledSentinel))
                {   // don't want to expose the singletons we used earlier
                    throw new TaskCanceledException();
                }
                catch (ObjectDisposedException ode) when (ReferenceEquals(ode, s_DisposedSentinel))
                {   // don't want to expose the singletons we used earlier
                    ThrowDisposed();
                    return default; // for compiler
                }
            }

            ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _moveNext.GetStatus(token);

            void IValueTaskSource<bool>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
                => _moveNext.OnCompleted(continuation, state, token, flags);

            int IValueTaskSource<int>.GetResult(short token) => _consumed.GetResult(token);

            ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token) => _consumed.GetStatus(token);

            void IValueTaskSource<int>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
                => _consumed.OnCompleted(continuation, state, token, flags);
        }
    }
}
