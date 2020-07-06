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

            foreach (var serviceContract in serviceContracts)
            {
                if (!binderConfiguration.Binder.IsServiceContract(serviceContract, out string? serviceName)) continue;

                var serviceDescriptor = new ServiceDescriptorProto
                {
                    Name = ServiceBinder.GetNameParts(serviceName, serviceType, out var package),
                };

                var dependencies = new HashSet<string>();
                foreach (var op in ContractOperation.FindOperations(binderConfiguration, serviceContract, null))
                {
                    // TODO: Validate op
                    if (TryGetType(binderConfiguration, op.From, fileDescriptorSet, out var inputType, out var inputFile)
                        && TryGetType(binderConfiguration, op.From, fileDescriptorSet, out var outputType, out var outputFile))
                    {
                        serviceDescriptor.Methods.Add(new MethodDescriptorProto
                        {
                            Name = op.Name,
                            InputType = inputType,
                            OutputType = outputType,
                            ClientStreaming = op.MethodType == MethodType.ClientStreaming ||
                                              op.MethodType == MethodType.DuplexStreaming,
                            ServerStreaming = op.MethodType == MethodType.ServerStreaming ||
                                              op.MethodType == MethodType.DuplexStreaming
                        });

                        dependencies.Add(inputFile);
                        dependencies.Add(outputFile);
                    }
                }

                var fileDescriptor = new FileDescriptorProto
                {
                    Name = serviceName + ".proto",
                    Services = { serviceDescriptor },
                    Syntax = "proto3",
                    Package = package
                };

                foreach (var dependency in dependencies)
                {
                    fileDescriptor.Dependencies.Add(dependency);
                    fileDescriptor.AddImport(dependency, true, default);
                }

                fileDescriptorSet.Files.Add(fileDescriptor);
            }
        }

        private static bool TryGetType(BinderConfiguration binderConfiguration, Type type, FileDescriptorSet fileDescriptorSet, out string fullyQualifiedName, out string descriptorProto)
        {
            var typeName = type.Name;
            var fileName = type.FullName + ".proto";
            var fileDescriptor = fileDescriptorSet.Files.SingleOrDefault(f => f.Name.Equals(fileName, StringComparison.Ordinal));

            TypeModel model = binderConfiguration.MarshallerCache.TryGetFactory(type) is ProtoBufMarshallerFactory factory ? factory.Model : RuntimeTypeModel.Default;

            if (fileDescriptor is null)
            {
                var schema = model.GetSchema(type, ProtoSyntax.Proto3);

                using var reader = new StringReader(schema);

                fileDescriptorSet.Add(fileName, includeInOutput: true, reader);
                fileDescriptor = fileDescriptorSet.Files.SingleOrDefault(f => f.Name.Equals(fileName, StringComparison.Ordinal));
                if (fileDescriptor is null)
                {
                    fullyQualifiedName = descriptorProto = "";
                    return false;
                }
            }

            var msgType = fileDescriptor.MessageTypes.SingleOrDefault(m => m.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
            if (msgType is null)
            {
                fullyQualifiedName = descriptorProto = "";
                return false;
            }
            descriptorProto = fileDescriptor.Name;
            fullyQualifiedName = "." + fileDescriptor.Package + "." + msgType.Name;
            return true;
        }
    }
}
