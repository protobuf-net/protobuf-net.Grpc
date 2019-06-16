using Grpc.Core;

namespace ProtoBuf.Grpc.Internal
{
    public class FullyNamedMethod<TRequest, TResponse> : Method<TRequest, TResponse>, IMethod
    {
        private readonly string _fullName;

        public FullyNamedMethod(
           string operationName,
           MethodType type,
           string serviceName,
           string? methodName = null,
           Marshaller<TRequest>? requestMarshaller = null,
           Marshaller<TResponse>? responseMarshaller = null)
           : base(type, serviceName, methodName ?? operationName,
                 requestMarshaller ?? MarshallerCache<TRequest>.Instance,
                 responseMarshaller ?? MarshallerCache<TResponse>.Instance)
        {
            _fullName = serviceName + "/" + operationName;
        }

        string IMethod.FullName => _fullName;
    }
}
