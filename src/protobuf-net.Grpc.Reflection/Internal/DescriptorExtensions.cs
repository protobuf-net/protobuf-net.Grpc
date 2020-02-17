using Google.Protobuf.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace ProtoBuf.Grpc.Reflection.Internal
{
    internal static class DescriptorExtensions
    {
        public static FileDescriptorProto GetFile(this IType type)
        {
            if (type is FileDescriptorProto file)
            {
                return file;
            }

            return type.Parent.GetFile();
        }

        public static IEnumerable<FileDescriptorProto> GetDependencies(this FileDescriptorProto file)
        {
            return file.Dependencies.Select(path => file.Parent.GetFile(file, path));
        }
    }
}
