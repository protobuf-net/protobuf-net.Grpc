using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using grpc.reflection.v1alpha;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection.Internal;

namespace ProtoBuf.Grpc.Reflection
{
    public sealed class ReflectionService : IServerReflection
    {
        private readonly List<string> _services;
        private readonly SymbolRegistry _symbolRegistry;

        public ReflectionService(params Type[] types)
            : this(null, types)
        {
        }

        public ReflectionService(BinderConfiguration? binderConfiguration, params Type[] types)
            : this(types.SelectMany(type => ServiceDescriptorFactory.Instance.GetServiceDescriptors(type, binderConfiguration)))
        {
        }

        public ReflectionService(
            IEnumerable<ServiceDescriptorProto> serviceDescriptors)
        {
            var descriptors = serviceDescriptors as ServiceDescriptorProto[] ?? serviceDescriptors.ToArray();
            _services = new List<string>(descriptors.Select((descriptor) => descriptor.FullyQualifiedName));
            _symbolRegistry = SymbolRegistry.FromFiles(descriptors.Select((descriptor) => descriptor.GetFile()));
        }

        public async IAsyncEnumerable<ServerReflectionResponse> ServerReflectionInfoAsync(IAsyncEnumerable<ServerReflectionRequest> requests, CallContext context = default)
        {
            await foreach(var request in requests)
            {
                var response = ProcessRequest(request);
                yield return response;
            }
        }

        private ServerReflectionResponse ProcessRequest(ServerReflectionRequest request)
        {
            switch (request.MessageRequestCase)
            {
                case ServerReflectionRequest.MessageRequestOneofCase.FileByFilename:
                    return FileByFilename(request.FileByFilename);
                case ServerReflectionRequest.MessageRequestOneofCase.FileContainingSymbol:
                    return FileContainingSymbol(request.FileContainingSymbol);
                case ServerReflectionRequest.MessageRequestOneofCase.ListServices:
                    return ListServices();
                case ServerReflectionRequest.MessageRequestOneofCase.AllExtensionNumbersOfType:
                case ServerReflectionRequest.MessageRequestOneofCase.FileContainingExtension:
                default:
                    return CreateErrorResponse(StatusCode.Unimplemented, "Request type not supported by C# reflection service.");
            }
        }

        private ServerReflectionResponse FileByFilename(string filename)
        {
            var file = _symbolRegistry.FileByName(filename);
            if (file == null)
            {
                return CreateErrorResponse(StatusCode.NotFound, "File not found.");
            }

            return new ServerReflectionResponse
            {
                FileDescriptorResponse = GetFileDescriptorResponse(file)
            };
        }

        private ServerReflectionResponse FileContainingSymbol(string symbol)
        {
            var file = _symbolRegistry.FileContainingSymbol(symbol);
            if (file == null)
            {
                return CreateErrorResponse(StatusCode.NotFound, "Symbol not found.");
            }

            return new ServerReflectionResponse
            {
                FileDescriptorResponse = GetFileDescriptorResponse(file)
            };
        }

        private FileDescriptorResponse GetFileDescriptorResponse(FileDescriptorProto file)
        {
            var transitiveDependencies = new HashSet<FileDescriptorProto>();
            CollectTransitiveDependencies(file, transitiveDependencies);

            var response = new FileDescriptorResponse();
            response.FileDescriptorProtoes.AddRange(transitiveDependencies.Select(Serialize));

            return response;
        }

        private ServerReflectionResponse ListServices()
        {
            var serviceResponses = new ListServiceResponse();
            foreach (string serviceName in _services)
            {
                serviceResponses.Services.Add(new ServiceResponse { Name = serviceName });
            }

            return new ServerReflectionResponse
            {
                ListServicesResponse = serviceResponses
            };
        }

        private ServerReflectionResponse CreateErrorResponse(StatusCode status, string message)
        {
            return new ServerReflectionResponse
            {
                ErrorResponse = new ErrorResponse { ErrorCode = (int)status, ErrorMessage = message }
            };
        }

        private void CollectTransitiveDependencies(FileDescriptorProto descriptor, HashSet<FileDescriptorProto> pool)
        {
            pool.Add(descriptor);
            foreach (var dependency in descriptor.GetDependencies())
            {
                if (pool.Add(dependency))
                {
                    // descriptors cannot have circular dependencies
                    CollectTransitiveDependencies(dependency, pool);
                }
            }
        }

        private byte[] Serialize(FileDescriptorProto fileDescriptor)
        {
            using var memoryStrem = new MemoryStream();
            Serializer.Serialize(memoryStrem, fileDescriptor);

            return memoryStrem.ToArray();
        }
    }
}
