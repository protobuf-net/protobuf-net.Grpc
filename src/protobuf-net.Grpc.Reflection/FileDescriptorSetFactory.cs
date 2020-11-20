using Google.Protobuf.Reflection;
using grpc.reflection.v1alpha;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using ProtoBuf.Grpc.Reflection.Internal;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Reflection
{
    internal static class FileDescriptorSetFactory
    {
        internal static FileDescriptorSet Create(IEnumerable<Type> serviceTypes, BinderConfiguration? binderConfiguration = null)
        {
            var fileDescriptorSet = new FileDescriptorSet();
            binderConfiguration ??= BinderConfiguration.Default;

            foreach (var serviceType in serviceTypes)
            {
                Populate(fileDescriptorSet, serviceType, binderConfiguration);
            }

            fileDescriptorSet.Process();
            return fileDescriptorSet;
        }

        private static void Populate(FileDescriptorSet fileDescriptorSet, Type serviceType, BinderConfiguration binderConfiguration)
        {
            var serviceContracts = typeof(IGrpcService).IsAssignableFrom(serviceType)
                ? new HashSet<Type> { serviceType }
                : ContractOperation.ExpandInterfaces(serviceType);
            var schemaGenerator = new SchemaGenerator
            {
                BinderConfiguration = binderConfiguration,
                ProtoSyntax = ProtoSyntax.Proto3
            };

            foreach (var serviceContract in serviceContracts)
            {
                if (!binderConfiguration.Binder.IsServiceContract(serviceContract, out var serviceName)) continue;
                
                var schema = schemaGenerator.GetSchema(serviceContract);
                using var reader = new StringReader(schema);

                fileDescriptorSet.Add(serviceName + ".proto", includeInOutput: true, reader);
            }
        }
    }
}
