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
    /// <summary>
    /// Implements the <see cref="IServerReflection"/> API
    /// </summary>
    public sealed class ReflectionService : IServerReflection
    {
        private readonly List<string> _services;
        private readonly SymbolRegistry _symbolRegistry;

        /// <summary>
        /// Creates a new <see cref="ReflectionService"/> instance
        /// </summary>
        public ReflectionService(params Type[] types)
            : this(null, types)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ReflectionService"/> instance
        /// </summary>
        public ReflectionService(BinderConfiguration? binderConfiguration, params Type[] types)
            : this(FileDescriptorSetFactory.Create(types, binderConfiguration))
        {
        }

        /// <summary>
        /// Creates a new <see cref="ReflectionService"/> instance
        /// </summary>
        public ReflectionService(
            FileDescriptorSet fileDescriptorSet)
        {
            _services = fileDescriptorSet.Files
                .SelectMany(x => x.Services)
                .Select(x => x.FullyQualifiedName.TrimStart('.'))
                .ToList();
            _symbolRegistry = SymbolRegistry.FromFiles(fileDescriptorSet);
        }

        async IAsyncEnumerable<ServerReflectionResponse> IServerReflection.ServerReflectionInfoAsync(IAsyncEnumerable<ServerReflectionRequest> requests, CallContext context)
        {
            await foreach(var request in requests)
            {
                var response = ProcessRequest(request);
                yield return response;
            }
        }

        private ServerReflectionResponse ProcessRequest(ServerReflectionRequest request) => request.MessageRequestCase switch
        {
            ServerReflectionRequest.MessageRequestOneofCase.FileByFilename => FileByFilename(request.FileByFilename),
            ServerReflectionRequest.MessageRequestOneofCase.FileContainingSymbol => FileContainingSymbol(request.FileContainingSymbol),
            ServerReflectionRequest.MessageRequestOneofCase.ListServices => ListServices(),
            _ => CreateErrorResponse(StatusCode.Unimplemented, "Request type not supported by C# reflection service."),
        };

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
            var file = _symbolRegistry.FileContainingSymbol("." + symbol.TrimStart('.'));
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
            var transitiveDependencies = new SortedSet<FileDescriptorProto>(FileDescriptorProtoComparer.Instance);
            CollectTransitiveDependencies(file, transitiveDependencies);

            var response = new FileDescriptorResponse();
            response.FileDescriptorProtoes.AddRange(
                transitiveDependencies.Select(Serialize));

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

        private static ServerReflectionResponse CreateErrorResponse(StatusCode status, string message)
            => new ServerReflectionResponse
            {
                ErrorResponse = new ErrorResponse { ErrorCode = (int)status, ErrorMessage = message }
            };

        private void CollectTransitiveDependencies(FileDescriptorProto descriptor, ISet<FileDescriptorProto> pool)
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

        private class FileDescriptorProtoComparer : IComparer<FileDescriptorProto>
        {
            public static FileDescriptorProtoComparer Instance { get; } = new FileDescriptorProtoComparer();

            public int Compare(FileDescriptorProto? left, FileDescriptorProto? right)
            {
                if (left is null)
                {
                    return right is null ? 0 : -1;
                }
                if (right is null)
                {
                    return 1;
                }
                if (GetTransitiveDependencies(left).Contains(right.Name))
                {
                    return 1;
                }
                if (GetTransitiveDependencies(right).Contains(left.Name))
                {
                    return -1;
                }

                return string.Compare(left.Name, right.Name, StringComparison.Ordinal);

                static IReadOnlyCollection<string> GetTransitiveDependencies(FileDescriptorProto? descriptor)
                {
                    if (descriptor is null)
                    {
                        return Array.Empty<string>();
                    }

                    var dependencies = new List<string>();

                    foreach (var dependency in descriptor.GetDependencies())
                    {
                        dependencies.Add(dependency.Name);
                        dependencies.AddRange(GetTransitiveDependencies(dependency));
                    }

                    return dependencies;
                }
            }
        }
    }
}
