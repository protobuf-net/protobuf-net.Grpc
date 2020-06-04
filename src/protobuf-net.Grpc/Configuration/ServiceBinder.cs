using ProtoBuf.Grpc.Internal;
using System;
using System.Reflection;

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

            int cut;
            if (contractType.IsGenericType && (cut = serviceName.IndexOf('`')) >= 0)
            {
                var parts = GetGenericParts(contractType);
                serviceName = serviceName.Substring(0, cut)
                    + "_" + string.Join("_", parts);
            }

            return serviceName ?? "";
        }

        /// <summary>
        /// Gets the default name for a potential data-contract
        /// </summary>
        protected virtual string GetDataContractName(Type contractType)
        {
            var attribs = AttributeHelper.For(contractType, inherit: false);

            if (attribs.TryGetAnyNonWhitespaceString("ProtoBuf.ProtoContractAttribute", "Name", out var name)
                || attribs.TryGetAnyNonWhitespaceString("System.Runtime.Serialization.DataContractAttribute", "Name", out name))
            {
                return name;
            }
            return contractType.Name;
        }

        /// <summary>
        /// Gets the default name for a potential operation-contract
        /// </summary>
        protected virtual string GetDefaultName(MethodInfo method)
        {
            var opName = method.Name;
            if (opName.EndsWith("Async"))
            {
#pragma warning disable IDE0057 // not on all frameworks
                opName = opName.Substring(0, opName.Length - 5);
#pragma warning restore IDE0057
            }
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
            var attribs = AttributeHelper.For(contractType, inherit: true);
            if (attribs.IsDefined("ProtoBuf.Grpc.Configuration.ServiceAttribute"))
            {
                attribs.TryGetAnyNonWhitespaceString("ProtoBuf.Grpc.Configuration.ServiceAttribute", "Name", out serviceName);
            }
            else if (attribs.IsDefined("System.ServiceModel.ServiceContractAttribute"))
            {
                attribs.TryGetAnyNonWhitespaceString("System.ServiceModel.ServiceContractAttribute", "Name", out serviceName);
            }
            else
            {
                name = default;
                return false;
            }
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                serviceName = GetDefaultName(contractType);
            }
            else if (contractType.IsGenericType)
            {
                var parts = GetGenericParts(contractType);
                serviceName = string.Format(serviceName, parts);
            }
            name = serviceName;
            return !string.IsNullOrWhiteSpace(name);
        }

        private string[] GetGenericParts(Type contractType)
        {
            var args = contractType.GetGenericArguments();
            var parts = new string[args.Length];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = GetDataContractName(args[i]);
            return parts;
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
            var attribs = AttributeHelper.For(method, inherit: true);
            if (attribs.IsDefined("ProtoBuf.Grpc.Configuration.OperationAttribute"))
            {
                attribs.TryGetAnyNonWhitespaceString("ProtoBuf.Grpc.Configuration.OperationAttribute", "Name", out opName);
            }
            else if (attribs.IsDefined("System.ServiceModel.OperationContractAttribute"))
            {
                attribs.TryGetAnyNonWhitespaceString("System.ServiceModel.OperationContractAttribute", "Name", out opName);
            }
            if (string.IsNullOrWhiteSpace(opName))
                opName = GetDefaultName(method);
            name = opName;
            return !string.IsNullOrWhiteSpace(name);
        }
    }
}
