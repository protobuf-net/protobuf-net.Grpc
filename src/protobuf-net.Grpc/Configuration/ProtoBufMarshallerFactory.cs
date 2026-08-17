using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides protobuf-net implementation of a per-type marshaller
    /// </summary>
    public partial class ProtoBufMarshallerFactory : MarshallerFactory
    {
        /// <summary>
        /// Options that control protobuf-net marshalling
        /// </summary>
        [Flags]
        public enum Options
        {
            /// <summary>
            /// No options
            /// </summary>
            None = 0,
            /// <summary>
            /// Enforce that only contract-types should be allowed
            /// </summary>
            ContractTypesOnly = 1 << 0,
            /// <summary>
            /// Disable 'contextual' serializer usage (this means serializers that use <see cref="ReadOnlySequence{T}"/> or <see cref="IBufferWriter{T}"/>),
            /// using the legacy <see cref="T:byte[]" /> APIs.
            /// </summary>
            DisableContextualSerializer = 1 << 1,
        }

        /// <summary>
        /// Uses the default protobuf-net serializer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>RuntimeTypeModel.Default</c> builds its serializers by reflection and ref-emit, so it
        /// cannot work where dynamic code is unavailable. Naming it from a static initialiser also
        /// makes the whole reflective serializer machinery <em>reachable</em>, which ILC must then
        /// keep - and since <see cref="BinderConfiguration.Create"/> touches this to compare against
        /// the default factory set, even a fully compile-time model dragged it all in.
        /// </para>
        /// <para>
        /// The <c>RuntimeFeature.IsDynamicCodeSupported</c> test is the lever, rather than a
        /// <c>Requires*</c> annotation: ILC substitutes it for a constant and eliminates the arm
        /// <em>before</em> trim analysis, so the demand disappears instead of relocating to callers.
        /// Same reasoning protobuf-net's own <c>TypeModel.ResolveSerializer&lt;T&gt;</c> uses.
        /// </para>
        /// <para>
        /// The consequence is deliberate: under native AOT a consumer who has not supplied a
        /// compile-time model gets "no marshaller available" immediately and by name, instead of the
        /// same failure arriving via a swallowed <c>MakeGenericType</c> inside <c>DynamicStub</c>.
        /// Neither can work; this one says so.
        /// </para>
        /// </remarks>
        public static MarshallerFactory Default { get; } = CreateDefault();

        private static MarshallerFactory CreateDefault()
        {
#if NET
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
            {
                return UnavailableMarshallerFactory.Instance;
            }
#endif
            return new ProtoBufMarshallerFactory(RuntimeTypeModel.Default, Options.None, default);
        }

        /// <summary>
        /// Stands in for the runtime-model factory where dynamic code is unavailable. It can never
        /// serialize anything, so resolution falls through to whatever the caller supplied.
        /// </summary>
        private sealed class UnavailableMarshallerFactory : MarshallerFactory
        {
            public static readonly UnavailableMarshallerFactory Instance = new();
            private UnavailableMarshallerFactory() { }
            protected internal override bool CanSerialize(Type type) => false;
        }

        /// <summary>
        /// Provides support for <a href="https://www.nuget.org/packages/Google.Protobuf/">Google.Protobuf</a> types.
        /// </summary>
        public static MarshallerFactory GoogleProtobuf => GoogleProtobufMarshallerFactory.Default;

        /// <summary>
        /// Gets the model used by this instance.
        /// </summary>
        public TypeModel Model => _model;

        private readonly TypeModel _model;
        private readonly SerializationContext? _userState;
        private readonly Options _options;
        // note: these are the same *object*, but pre-checked for optional API support, for efficiency
        // (the minimum .NET object size means that the extra fields don't cost anything)
        private readonly IMeasuredProtoOutput<IBufferWriter<byte>>? _measuredWriterModel;
#pragma warning disable CA1859 // change type of field for performance - but actually this is a speculative test
        private readonly IProtoInput<ReadOnlySequence<byte>>? _squenceReaderModel;
#pragma warning restore CA1859

        /// <summary>
        /// Create a new factory using a specific protobuf-net model
        /// </summary>
        public static MarshallerFactory Create(TypeModel? model = null, Options options = Options.None, object? userState = null)
        {
            // the null/default-model path is the reflective one, so it is gated exactly as Default is;
            // naming RuntimeTypeModel.Default unconditionally here would re-root everything that the
            // gate on Default was there to remove
#if NET
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
            {
                if (model is null) return Default; // i.e. UnavailableMarshallerFactory
                return new ProtoBufMarshallerFactory(model, options, userState);
            }
#endif
            model ??= RuntimeTypeModel.Default;
            if (options == Options.None && model == RuntimeTypeModel.Default && userState is null) return Default;
            return new ProtoBufMarshallerFactory(model, options, userState);
        }

        /// <summary>
        /// Create a new factory using a specific protobuf-net model
        /// </summary>
        public static MarshallerFactory Create(RuntimeTypeModel? model, Options options)
            => Create((TypeModel?)model, options, null);

        internal ProtoBufMarshallerFactory(TypeModel model, Options options, object? userState = null)
        {
            _model = model;
            _options = options;
            _userState = userState switch
            {
                null => null,
                SerializationContext ctx => ctx,
                _ => new SerializationContext { Context = userState }
            };

            if (!HasSingle(Options.DisableContextualSerializer))
            {
                // test these once rather than every time
                _measuredWriterModel = model as IMeasuredProtoOutput<IBufferWriter<byte>>;
                _squenceReaderModel = model as IProtoInput<ReadOnlySequence<byte>>;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasSingle(Options option) => (_options & option) != 0;

        /// <summary>
        /// Deserializes an object from a payload
        /// </summary>
        protected internal override global::Grpc.Core.Marshaller<T> CreateMarshaller<T>()
            => HasSingle(Options.DisableContextualSerializer)
            ? base.CreateMarshaller<T>()
            : new global::Grpc.Core.Marshaller<T>(ContextualSerialize<T>, ContextualDeserialize<T>);

#if DEBUG
        private int _uplevelBufferReadCount, _uplevelBufferWriteCount;
        public int UplevelBufferReadCount => Volatile.Read(ref _uplevelBufferReadCount);
        public int UplevelBufferWriteCount => Volatile.Read(ref _uplevelBufferWriteCount);

        partial void RecordUplevelBufferRead() => Interlocked.Increment(ref _uplevelBufferReadCount);
        partial void RecordUplevelBufferWrite() => Interlocked.Increment(ref _uplevelBufferWriteCount);
#endif
        partial void RecordUplevelBufferRead();
        partial void RecordUplevelBufferWrite();

        private static bool TryGetBufferWriter(global::Grpc.Core.SerializationContext context, out IBufferWriter<byte>? writer)
        {
            // the managed implementation does not yet implement this API
            try { writer = context.GetBufferWriter(); }
            catch (NotSupportedException) { writer = default; }
            catch (NotImplementedException) { writer = default; }
            return writer is object;
        }

        private void ContextualSerialize<T>(T value, global::Grpc.Core.SerializationContext context)
        {
            long length;
            if (_measuredWriterModel is object)
            {   // forget what we think we know about TypeModel; if we have protobuf-net 3.*, we can do this

                RecordUplevelBufferWrite();

                using var measured = _measuredWriterModel.Measure(value, userState: _userState);
                length = measured.Length;
                int len = checked((int)length);
                context.SetPayloadLength(len);

                if (TryGetBufferWriter(context, out var writer))
                {   // write to the buffer-writer API
                    _measuredWriterModel.Serialize(measured, writer!);
                    context.Complete();
                }
                else
                {
                    // the buffer-writer API wasn't supported, but we can still optimize by right-sizing
                    // a MemoryStream to write to, to avoid a resize etc
                    context.Complete(Serialize<T>(value, len));
                }
            }
            else
            {
                var buffer = Serialize<T>(value);
                length = buffer.Length;
                context.Complete(buffer);
            }
            if (value is IPayloadLength withLength)
            {
                withLength.SetLength(length);
            }
        }

        private T ContextualDeserialize<T>(global::Grpc.Core.DeserializationContext context)
        {
            var ros = context.PayloadAsReadOnlySequence();
            T result;
            if (_squenceReaderModel is object)
            {   // forget what we think we know about TypeModel; if we have protobuf-net 3.*, we can do this
                RecordUplevelBufferRead();
                result = _squenceReaderModel.Deserialize<T>(ros, userState: _userState);
            }
            else
            {

                // 2.4.2+ can use array-segments
                IProtoInput<ArraySegment<byte>> segmentReader = _model;

                // can we go direct to a single segment?
                if (ros.IsSingleSegment && MemoryMarshal.TryGetArray(ros.First, out var segment))
                {
                    result = segmentReader.Deserialize<T>(segment, userState: _userState);
                }
                else
                {
                    // otherwise; linearize the data
                    var oversized = ArrayPool<byte>.Shared.Rent(context.PayloadLength);
                    try
                    {
                        ros.CopyTo(oversized);
                        result = segmentReader.Deserialize<T>(new ArraySegment<byte>(oversized, 0, context.PayloadLength), userState: _userState);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(oversized);
                    }
                }
            }
            if (result is IPayloadLength withLength)
            {
                withLength.SetLength(context.PayloadLength);
            }
            return result;
        }

        /// <summary>
        /// Indicates whether a type should be considered as a serializable data type
        /// </summary>
        protected internal override bool CanSerialize(Type type)
        {
            try
            {
                return HasSingle(Options.ContractTypesOnly)
                    ? _model.CanSerializeContractType(type)
                    : _model.CanSerialize(type);
            }
            catch (NotSupportedException)
            {
                // a typical case is the use of jagged arrays
                return false;
            }
        }

        /// <summary>
        /// Deserializes an object from a payload
        /// </summary>
        protected override T Deserialize<T>(byte[] payload)
        {
            IProtoInput<byte[]> segmentReader = _model;
            return segmentReader.Deserialize<T>(payload, userState: _userState);
        }

        /// <summary>
        /// Serializes an object to a payload
        /// </summary>
        protected override byte[] Serialize<T>(T value)
        {
            using var ms = new MemoryStream();
            _model.Serialize(ms, value, context: null);
            return ms.ToArray();
        }

        private byte[] Serialize<T>(T value, int length)
        {
            if (length == 0) return Array.Empty<byte>();
            var arr = new byte[length];
            using var ms = new MemoryStream(arr);
            _model.Serialize(ms, value, context: null);
            if (length != ms.Length) throw new InvalidOperationException(
                    $"Length miscalculated; expected {length}, got {ms.Length}");
            return arr;
        }
    }
}
