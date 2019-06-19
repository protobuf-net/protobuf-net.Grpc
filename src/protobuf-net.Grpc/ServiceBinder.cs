using System;
using System.Reflection;
using System.ServiceModel;

namespace ProtoBuf.Grpc
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
        /// Indicates whether an interface should be considered a service-contract (and if so: by what name)
        /// </summary>
        public virtual bool IsServiceContract(Type contractType, out string name)
        {
            var sca = (ServiceContractAttribute?)Attribute.GetCustomAttribute(contractType, typeof(ServiceContractAttribute), inherit: true);
            if (sca == null)
            {
                name = "";
                return false;
            }
            var serviceName = sca?.Name;
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                serviceName = contractType.Name;
                if (contractType.IsInterface && serviceName.StartsWith("I")) serviceName = serviceName.Substring(1); // IFoo => Foo
                serviceName = contractType.Namespace + "." + serviceName; // Whatever.Foo
                serviceName = serviceName.Replace('+', '.'); // nested types
            }
            name = serviceName ?? "";
            return !string.IsNullOrWhiteSpace(name);
        }

        /// <summary>
        /// Indicates whether a method should be considered an operation-contract (and if so: by what name)
        /// </summary>
        public virtual bool IsOperationContract(MethodInfo method, out string name)
        {
            var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
            string? opName = oca?.Name;
            if (string.IsNullOrWhiteSpace(opName))
            {
                opName = method.Name;
                if (opName.EndsWith("Async"))
#pragma warning disable IDE0057 // not on all frameworks
                    opName = opName.Substring(0, opName.Length - 5);
#pragma warning restore IDE0057
            }
            name = opName;
            return !string.IsNullOrWhiteSpace(opName);
        }
    }
}
