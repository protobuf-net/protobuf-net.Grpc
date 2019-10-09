using System;
using System.Linq;
using System.Reflection;
using System.ServiceModel;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Describes rules for binding to gRPC services
    /// </summary>
    public class ServiceBinder
    {
        /// <summary>
        /// Default bindings; services require ServiceContractAttribute; all operations are considered
        /// </summary>
        public static ServiceBinder Default { get; } = new ServiceBinder();
        /// <summary>
        /// Create a new instance
        /// </summary>
        protected ServiceBinder() { }

        /// <summary>
        /// Gets the default name for a potential service-contract
        /// </summary>
        protected virtual string GetDefaultName(Type contractType)
        {
            var serviceName = contractType.Name;
            if (contractType.IsInterface && serviceName.StartsWith("I")) serviceName = serviceName.Substring(1); // IFoo => Foo
            serviceName = contractType.Namespace + "." + serviceName; // Whatever.Foo
            serviceName = serviceName.Replace('+', '.'); // nested types

            return serviceName ?? "";
        }

        /// <summary>
        /// Gets the default name for a potential operation-contract
        /// </summary>
        protected virtual string GetDefaultName(MethodInfo method)
        {
            var opName = method.Name;
            if (opName.EndsWith("Async"))
#pragma warning disable IDE0057 // not on all frameworks
            opName = opName.Substring(0, opName.Length - 5);
#pragma warning restore IDE0057
            return opName ?? "";
        }

        /// <summary>
        /// Indicates whether an interface should be considered a service-contract (and if so: by what name)
        /// </summary>
        public virtual bool IsServiceContract(Type contractType, out string? name)
        {
            if (contractType.GetInterfaces().Any(x => x == typeof(IGrpcService)))
            {
                name = contractType.Name;
                return true;
            }

            var sca = (ServiceContractAttribute?)Attribute.GetCustomAttribute(contractType, typeof(ServiceContractAttribute), inherit: true);
            if (sca == null)
            {
                name = default;
                return false;
            }
            var serviceName = sca?.Name;
            if (string.IsNullOrWhiteSpace(serviceName))
                serviceName = GetDefaultName(contractType);
            name = serviceName;
            return !string.IsNullOrWhiteSpace(name);
        }

        /// <summary>
        /// Indicates whether a method should be considered an operation-contract (and if so: by what name)
        /// </summary>
        public virtual bool IsOperationContract(MethodInfo method, out string? name)
        {
            if (method.DeclaringType == typeof(object) || !method.IsPublic)
            {
                name = null;
                return false;
            }

            var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
            string? opName = oca?.Name;
            if (string.IsNullOrWhiteSpace(opName))
                opName = GetDefaultName(method);
            name = opName;
            return !string.IsNullOrWhiteSpace(name);
        }
    }
}
