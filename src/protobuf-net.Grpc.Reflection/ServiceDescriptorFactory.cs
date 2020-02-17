using Google.Protobuf.Reflection;
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
    internal class ServiceDescriptorFactory
    {
        private static Lazy<ServiceDescriptorFactory> _instance = new Lazy<ServiceDescriptorFactory>(() => new ServiceDescriptorFactory());

        private FileDescriptorSet _fileDescriptorSet;

        private ServiceDescriptorFactory()
        {
            _fileDescriptorSet = new FileDescriptorSet();
        }

        public static ServiceDescriptorFactory Instance => _instance.Value;

        internal IEnumerable<ServiceDescriptorProto> GetServiceDescriptors(Type serviceType, BinderConfiguration? binderConfiguration = null)
        {
            string? serviceName;
            binderConfiguration ??= BinderConfiguration.Default;
            var serviceContracts = typeof(IGrpcService).IsAssignableFrom(serviceType)
                ? new HashSet<Type> { serviceType }
                : ContractOperation.ExpandInterfaces(serviceType);
            var serviceDescriptors = new List<ServiceDescriptorProto>();

            foreach (var serviceContract in serviceContracts)
            {
                if (!binderConfiguration.Binder.IsServiceContract(serviceContract, out serviceName)) continue;

                var serviceDescriptor = new ServiceDescriptorProto
                {
                    Name = serviceName
                };

                var dependencies = new Dictionary<string, FileDescriptorProto>();
                foreach (var op in ContractOperation.FindOperations(binderConfiguration, serviceContract))
                {
                    // TODO: Validate op
                    serviceDescriptor.Methods.Add(new MethodDescriptorProto
                    {
                        Name = op.Name,
                        InputType = GetType(op.From, out var inputDescriptor),
                        OutputType = GetType(op.To, out var outputDescriptor),
                        ClientStreaming = op.MethodType == MethodType.ClientStreaming ||
                                          op.MethodType == MethodType.DuplexStreaming,
                        ServerStreaming = op.MethodType == MethodType.ServerStreaming ||
                                          op.MethodType == MethodType.DuplexStreaming
                    });

                    // TODO: We probably don't want to process this every time.
                    _fileDescriptorSet.Process();

                    if (!dependencies.ContainsKey(inputDescriptor.FullyQualifiedName))
                    {
                        dependencies.Add(inputDescriptor.FullyQualifiedName, inputDescriptor.GetFile());
                    }
                    if (!dependencies.ContainsKey(outputDescriptor.FullyQualifiedName))
                    {
                        dependencies.Add(outputDescriptor.FullyQualifiedName, outputDescriptor.GetFile());
                    }
                }

                var fileDescriptor = new FileDescriptorProto
                {
                    Name = serviceName + ".proto",
                    Services = { serviceDescriptor }
                };
                fileDescriptor.Dependencies.AddRange(dependencies.Values.Select(x => x.Name));

                _fileDescriptorSet.Files.Add(fileDescriptor);
            }

            return serviceDescriptors;
        }

        private string GetType(Type type, out DescriptorProto descriptorProto)
        {
            var fileName = type.FullName + ".proto";
            var fileDescriptor = _fileDescriptorSet.Files.SingleOrDefault(f => f.Name.Equals(fileName, StringComparison.Ordinal));

            if (fileDescriptor is null)
            {
                var schema = RuntimeTypeModel.Default.GetSchema(type, ProtoSyntax.Proto3);

                using var reader = new StringReader(schema);

                _fileDescriptorSet.Add(fileName, includeInOutput: true, reader);
                fileDescriptor = _fileDescriptorSet.Files.Single(f => f.Name.Equals(fileName, StringComparison.Ordinal));
            }

            descriptorProto = fileDescriptor.MessageTypes.Single();

            return fileDescriptor.Package + "." + descriptorProto.Name;
        }
    }
}
