using System;
using System.Collections.Generic;
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
        ValueTask PushAsync(T value);
    }

    /// <summary>
    /// Controls the behavior of the <see cref="PushAsyncEnumerable"/> instance
    /// </summary>
    [Flags]
    public enum PushAsyncFlags
    {
        /// <summary>
        /// Default options
        /// </summary>
        None = 0,
        /// <summary>
        /// Allows <see cref="IPushAsyncEnumerable{T}.PushAsync(T)"/> to synchronously reactivate the
        /// consumer, rather than queueing the reactivation asynchronously (the default)
        /// </summary>
        InlineProducerToConsumer = 1 << 0,
        /// <summary>
        /// Allos <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> to synchrously reactivate the
        /// producer, rather than queueing the reactivation asynchronously (the default)
        /// </summary>
        InlineConsumerToProducer = 1 << 1,
    }

    /// <summary>
    /// Allows construction of <see cref="IPushAsyncEnumerable{T}"/> instances
    /// </summary>
    public static class PushAsyncEnumerable
    {
        /// <summary>
        /// Create a new <see cref="IPushAsyncEnumerable{T}"/> instance
        /// </summary>
        public static IPushAsyncEnumerable<T> Create<T>(PushAsyncFlags flags = PushAsyncFlags.None, CancellationToken cancellationToken = default) => new PushAsyncEnumerableCore<T>(flags, cancellationToken);

        private static readonly Exception
            s_CanceledSentinel = new OperationCanceledException(),
            s_CompletedSentinel = new InvalidOperationException();

        private static readonly Action<object> s_CancelCallback = s => (s as ICancellableInner)?.Cancel();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TrySetException<T>(ref ManualResetValueTaskSourceCore<T> source, Exception exception)
        {
            try
            {
                source.SetException(exception);
            }
            catch { }
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowSingleEnumerator() => throw new NotSupportedException(nameof(IAsyncEnumerable<int>.GetAsyncEnumerator) + " should only be called once per instance");

        private interface ICancellableInner
        {
            void Cancel();
        }

        private static CancellationTokenRegistration RegisterForCancellation(ICancellableInner obj, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return default;

            if (cancellationToken.IsCancellationRequested)
            {
                obj.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return cancellationToken.Register(s_CancelCallback, obj, false);
        }

        private static void UnregisterForCancellation(ref CancellationTokenRegistration field)
        {
            var tmp = field;
            field = default;
            try { tmp.Dispose(); }
            catch { }
        }

        [Flags]
        private enum StateFlags
        {
            None = 0,
            IsCompleted = 1 << 0,
            HasActiveEnumerator = 1 << 1,
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasStateFlag(ref int state, StateFlags flag)
            => (Volatile.Read(ref state) & (int)flag) != 0;

        private static bool SetStateFlag(ref int state, StateFlags flag)
        {
            int oldValue = Volatile.Read(ref state);
            while (true)
            {
                // find the new value; if it is already that: we didn't make the change
                var newValue = oldValue | (int)flag;
                if (newValue == oldValue) return false;

                // if we can make a sweap, and the old value hasn't changed: we made the change
                var was = Interlocked.CompareExchange(ref state, newValue, oldValue);
                if (was == oldValue) return true;

                // retry, with our new knowledge
                oldValue = was;
            }
        }

        private sealed class PushAsyncEnumerableCore<T> : ICancellableInner,
            IPushAsyncEnumerable<T>, IAsyncEnumerator<T>,
            IValueTaskSource<bool>, IValueTaskSource
        {
            private ManualResetValueTaskSourceCore<bool> _moveNext;
            private ManualResetValueTaskSourceCore<int> _consumed;
            private CancellationToken _globalCancellationToken;
            private CancellationTokenRegistration _globalCancellationTokenRegistration, _iteratorCancellationTokenRegistration;
            private int _stateFlags; // actually a StateFlags, but: Interlocked doesn't like that

            private T _current, _next;

            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => HasStateFlag(ref _stateFlags, StateFlags.IsCompleted);
            }

            public PushAsyncEnumerableCore(PushAsyncFlags flags, CancellationToken cancellationToken)
            {
                _current = _next = default!;
                _consumed.RunContinuationsAsynchronously = (flags & PushAsyncFlags.InlineConsumerToProducer) == 0;
                _moveNext.RunContinuationsAsynchronously = (flags & PushAsyncFlags.InlineProducerToConsumer) == 0;

                _globalCancellationTokenRegistration = RegisterForCancellation(this, cancellationToken);
            }

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
                UnregisterForCancellation(ref _globalCancellationTokenRegistration);
                UnregisterForCancellation(ref _iteratorCancellationTokenRegistration);
                _globalCancellationToken = default;

                // set outcomes
                if (error is null)
                {
                    try { _moveNext.SetResult(false); } catch { }
                    TrySetException(ref _consumed, s_CompletedSentinel);
                }
                else
                {
                    TrySetException(ref _moveNext, error);
                    TrySetException(ref _consumed, error);
                }
                SetStateFlag(ref _stateFlags, StateFlags.IsCompleted);
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

            IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                if (!SetStateFlag(ref _stateFlags, StateFlags.HasActiveEnumerator))
                {
                    ThrowSingleEnumerator();
                }
                if (cancellationToken != _globalCancellationToken)
                {
                    _iteratorCancellationTokenRegistration = RegisterForCancellation(this, cancellationToken);
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
