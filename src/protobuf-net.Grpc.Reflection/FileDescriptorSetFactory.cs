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

namespace ProtoBuf.Grpc.Reflection
{
    internal static class FileDescriptorSetFactory
    {
        internal static FileDescriptorSet Create(IEnumerable<Type> serviceTypes, BinderConfiguration? binderConfiguration = null)
        {
            var fileDescriptorSet = new FileDescriptorSet();
            var configuration = binderConfiguration ?? BinderConfiguration.Default;

            foreach (var serviceType in serviceTypes)
            {
                Populate(fileDescriptorSet, serviceType, configuration);
            }

            fileDescriptorSet.Process();
            return fileDescriptorSet;
        }

        private static void Populate(FileDescriptorSet fileDescriptorSet, Type serviceType, BinderConfiguration binderConfiguration)
        {
            if (typeof(IServerReflection).IsAssignableFrom(serviceType))
            {
                return;
            }

            string? serviceName, inputFile, outputFile;
            var serviceContracts = typeof(IGrpcService).IsAssignableFrom(serviceType)
                ? new HashSet<Type> { serviceType }
                : ContractOperation.ExpandInterfaces(serviceType);

            foreach (var serviceContract in serviceContracts)
            {
                if (!binderConfiguration.Binder.IsServiceContract(serviceContract, out serviceName)) continue;

                var serviceDescriptor = new ServiceDescriptorProto
                {
                    Name = serviceName!.Split('.').Last()
                };

                var dependencies = new HashSet<string>();
                foreach (var op in ContractOperation.FindOperations(binderConfiguration, serviceContract, null))
                {
                    // TODO: Validate op
                    serviceDescriptor.Methods.Add(new MethodDescriptorProto
                    {
                        Name = op.Name,
                        InputType = GetType(op.From, fileDescriptorSet, out inputFile),
                        OutputType = GetType(op.To, fileDescriptorSet, out outputFile),
                        ClientStreaming = op.MethodType == MethodType.ClientStreaming ||
                                          op.MethodType == MethodType.DuplexStreaming,
                        ServerStreaming = op.MethodType == MethodType.ServerStreaming ||
                                          op.MethodType == MethodType.DuplexStreaming
                    });

                    dependencies.Add(inputFile);
                    dependencies.Add(outputFile);
                }

                var fileDescriptor = new FileDescriptorProto
                {
                    Name = serviceName + ".proto",
                    Services = { serviceDescriptor },
                    Syntax = "proto3",
                    Package = serviceContract.Namespace
                };

                foreach (var dependency in dependencies)
                {
                    fileDescriptor.Dependencies.Add(dependency);
                    fileDescriptor.AddImport(dependency, true, default);
                }

                fileDescriptorSet.Files.Add(fileDescriptor);
            }
        }

        private static string GetType(Type type, FileDescriptorSet fileDescriptorSet, out string descriptorProto)
        {
            var fileName = type.FullName + ".proto";
            var fileDescriptor = fileDescriptorSet.Files.SingleOrDefault(f => f.Name.Equals(fileName, StringComparison.Ordinal));

            if (fileDescriptor is null)
            {
                var schema = RuntimeTypeModel.Default.GetSchema(type, ProtoSyntax.Proto3);

                using var reader = new StringReader(schema);

                fileDescriptorSet.Add(fileName, includeInOutput: true, reader);
                fileDescriptor = fileDescriptorSet.Files.Single(f => f.Name.Equals(fileName, StringComparison.Ordinal));
            }

            descriptorProto = fileDescriptor.Name;

            return "." + fileDescriptor.Package + "." + fileDescriptor.MessageTypes.Single().Name;
        }
    }
}
