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
            if (typeof(IGrpcService).IsAssignableFrom(contractType))
            {
                name = contractType.Name;
                return true;
            }

            string? serviceName = null;
            var attribs = Attribute.GetCustomAttributes(contractType, inherit: true);
            var sa = attribs.OfType<ServiceAttribute>().FirstOrDefault();
            if (sa == null)
            {
                // note: uses runtime discovery instead of hard ref because of bind/load problems
                var sca = attribs.FirstOrDefault(x => x.GetType().FullName == "System.ServiceModel.ServiceContractAttribute");
                if (sca == null)
                {
                    name = default;
                    return false;
                }
                TryGetProperty(sca, "Name", out serviceName);
            }
            else
            {
                serviceName = sa.Name;
            }
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

            string? opName = null;
            var attribs = Attribute.GetCustomAttributes(method, inherit: true);
            var oa = attribs.OfType<OperationAttribute>().FirstOrDefault();
            if (oa == null)
            {
                // note: uses runtime discovery instead of hard ref because of bind/load problems
                var oca = attribs
                    .FirstOrDefault(x => x.GetType().FullName == "System.ServiceModel.OperationContractAttribute");
                TryGetProperty(oca, "Name", out opName);
            }
            else
            {
                opName = oa.Name;
            }
            if (string.IsNullOrWhiteSpace(opName))
                opName = GetDefaultName(method);
            name = opName;
            return !string.IsNullOrWhiteSpace(name);
        }

        static bool TryGetProperty<T>(Attribute obj, string name, out T value)
        {
            value = default!;
            if (obj != null)
            {
                var prop = obj.GetType().GetProperty(name);
                if (prop != null)
                {
                    if (prop.GetValue(obj) is T typed)
                    {
                        value = typed;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
