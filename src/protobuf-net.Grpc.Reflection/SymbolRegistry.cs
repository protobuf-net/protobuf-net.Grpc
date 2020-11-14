// Derived from:
//     https://github.com/grpc/grpc/blob/64fb7d47452e32e8746569bf0d1c19c5d1f1a1d9/src/csharp/Grpc.Reflection/SymbolRegistry.cs
//
// Copyright 2015 gRPC authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using Grpc.Core.Utils;
using Google.Protobuf.Reflection;
using ProtoBuf.Grpc.Reflection.Internal;
using System.Linq;

namespace ProtoBuf.Grpc.Reflection
{
    /// <summary>Registry of protobuf symbols</summary>
    public class SymbolRegistry
    {
        private readonly Dictionary<string, FileDescriptorProto> filesByName;
        private readonly Dictionary<string, FileDescriptorProto> filesBySymbol;

        private SymbolRegistry(Dictionary<string, FileDescriptorProto> filesByName, Dictionary<string, FileDescriptorProto> filesBySymbol)
        {
            this.filesByName = new Dictionary<string, FileDescriptorProto>(filesByName);
            this.filesBySymbol = new Dictionary<string, FileDescriptorProto>(filesBySymbol);
        }

        /// <summary>
        /// Creates a symbol registry from the specified set of file descriptors.
        /// </summary>
        /// <param name="fileDescriptorSet">The set of files to include in the registry. Must not contain null values.</param>
        /// <returns>A symbol registry for the given files.</returns>
        public static SymbolRegistry FromFiles(FileDescriptorSet fileDescriptorSet)
        {
            GrpcPreconditions.CheckNotNull(fileDescriptorSet);
            var builder = new Builder();
            foreach (var file in fileDescriptorSet.Files)
            {
                builder.AddFile(file);
            }
            return builder.Build();
        }

        /// <summary>
        /// Gets file descriptor for given file name (including package path). Returns <c>null</c> if not found.
        /// </summary>
        public FileDescriptorProto? FileByName(string filename)
            => filesByName.TryGetValue(filename, out var file) ? file : default;

        /// <summary>
        /// Gets file descriptor that contains definition of given symbol full name (including package path). Returns <c>null</c> if not found.
        /// </summary>
        public FileDescriptorProto? FileContainingSymbol(string symbol)
            => filesBySymbol.TryGetValue(symbol, out var file) ? file : default;

        /// <summary>
        /// Builder class which isn't exposed, but acts as a convenient alternative to passing round two dictionaries in recursive calls.
        /// </summary>
        private class Builder
        {
            private readonly Dictionary<string, FileDescriptorProto> _filesByName;
            private readonly Dictionary<string, FileDescriptorProto> _filesBySymbol;


            internal Builder()
            {
                _filesByName = new Dictionary<string, FileDescriptorProto>();
                _filesBySymbol = new Dictionary<string, FileDescriptorProto>();
            }

            internal void AddFile(FileDescriptorProto fileDescriptor)
            {
                if (_filesByName.ContainsKey(fileDescriptor.Name))
                {
                    return;
                }
                _filesByName.Add(fileDescriptor.Name, fileDescriptor);

                foreach (var dependency in fileDescriptor.GetDependencies())
                {
                    AddFile(dependency);
                }
                foreach (var enumeration in fileDescriptor.EnumTypes)
                {
                    AddEnum(enumeration);
                }
                foreach (var message in fileDescriptor.MessageTypes)
                {
                    AddMessage(message);
                }
                foreach (var service in fileDescriptor.Services)
                {
                    AddService(service);
                }
            }

            private void AddEnum(EnumDescriptorProto enumDescriptor)
            {
                _filesBySymbol[enumDescriptor.FullyQualifiedName] = enumDescriptor.GetFile();
            }

            private void AddMessage(DescriptorProto messageDescriptor)
            {
                foreach (var nestedEnum in messageDescriptor.EnumTypes)
                {
                    AddEnum(nestedEnum);
                }
                foreach (var nestedType in messageDescriptor.NestedTypes)
                {
                    AddMessage(nestedType);
                }
                _filesBySymbol[messageDescriptor.FullyQualifiedName] = messageDescriptor.GetFile();
            }

            private void AddService(ServiceDescriptorProto serviceDescriptor)
            {
                foreach (var method in serviceDescriptor.Methods)
                {
                    _filesBySymbol[method.FullyQualifiedName] = method.GetFile();
                }
                _filesBySymbol[serviceDescriptor.FullyQualifiedName] = serviceDescriptor.GetFile();
            }

            internal SymbolRegistry Build()
            {
                return new SymbolRegistry(_filesByName, _filesBySymbol);
            }
        }
    }
}
