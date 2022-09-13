using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private static readonly ConcurrentDictionary<Type, InterfaceMapping> s_map = new ConcurrentDictionary<Type, InterfaceMapping>();
        private static InterfaceMapping GetMap(Type contractType, Type serviceType)
        {
            if (!s_map.TryGetValue(contractType, out var interfaceMapping))
            {   // note: it doesn't matter if this ends up getting called more than once
                // in a race condition - we don't need to block etc (the result will be compatible)
                interfaceMapping = serviceType.GetInterfaceMap(contractType);
                s_map[contractType] = interfaceMapping;
            }
            return interfaceMapping;
        }

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
                opName = opName.Substring(0, opName.Length - 5);
            }
            return opName ?? "";
        }

        internal static string GetNameParts(string? declaredName, Type? type, out string package)
        {
            declaredName ??= "";
            var idx = declaredName.LastIndexOf('.');
            if (idx >= 0)
            {
                package = declaredName.Substring(0, idx);
                return declaredName.Substring(idx + 1);
            }
            package = type?.Namespace ?? "";
            return declaredName;
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

            string? serviceName;
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

        /// <summary>
        /// <para>Gets the metadata associated with a specific contract method.</para>
        /// <para>Note: Later is higher priority in the code that consumes this.</para>
        /// </summary>
        /// <returns>Prioritised list of metadata.</returns>
        public virtual IList<object> GetMetadata(MethodInfo method, Type contractType, Type serviceType)
        {
            // consider the various possible sources of distinct metadata
            object[]
                contractTypeAtt = contractType.GetCustomAttributes(inherit: true),
                contractMethodAtt = method.GetCustomAttributes(inherit: true),
                serviceTypeAtt = Array.Empty<object>(),
                serviceMethodAtt = Array.Empty<object>();
            if (contractType != serviceType & contractType.IsInterface & serviceType.IsClass)
            {
                serviceTypeAtt = serviceType.GetCustomAttributes(inherit: true);
                serviceMethodAtt = GetMethodImplementation(method, contractType, serviceType)?.GetCustomAttributes(inherit: true)
                    ?? Array.Empty<object>();
            }

            // note: later is higher priority in the code that consumes this, but
            // GetAttributes() is "most derived to least derived", so: add everything
            // backwards, then reverse
            var metadata = new List<object>(
                contractTypeAtt.Length + contractMethodAtt.Length +
                serviceTypeAtt.Length + serviceMethodAtt.Length);

            metadata.AddRange(serviceMethodAtt);
            metadata.AddRange(serviceTypeAtt);
            metadata.AddRange(contractMethodAtt);
            metadata.AddRange(contractTypeAtt);
            metadata.Reverse();
            return metadata;
        }

        /// <summary>
        /// Gets the implementing method from a method definition
        /// </summary>
        public MethodInfo? GetMethodImplementation(MethodInfo serviceMethod, Type contractType, Type serviceType)
        {
            if (contractType != serviceType & serviceMethod is object)
            {
                var map = GetMap(contractType, serviceType);
                var from = map.InterfaceMethods;
                var to = map.TargetMethods;
                int end = Math.Min(from.Length, to.Length);
                for (int i = 0; i < end; i++)
                {
                    if (from[i] == serviceMethod) return to[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Attempt to resolve a sub-service
        /// </summary>
        /// <param name="type"></param>
        /// <param name="typesToSearchIn"></param>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        internal bool TryFindInheritedService(Type type, IEnumerable<Type> typesToSearchIn, out string? serviceName)
        {
            foreach (var potentialServiceContract in typesToSearchIn)
            {
                if (potentialServiceContract != type
                    && type.IsAssignableFrom(potentialServiceContract)
                    && IsServiceContract(potentialServiceContract, out serviceName))
                {
                    return true;
                }
            }

            serviceName = null;
            return false;
        }
    }
}
