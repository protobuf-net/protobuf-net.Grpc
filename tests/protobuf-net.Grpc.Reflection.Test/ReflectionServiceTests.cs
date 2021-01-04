using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf.Reflection;
using grpc.reflection.v1alpha;
using ProtoBuf;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection;
using ProtoBuf.Grpc.Reflection.Internal;
using Xunit;

namespace protobuf_net.Grpc.Reflection.Test
{
    using ReflectionTest;

    public class ReflectionServiceTests
    {
        private static Lazy<MethodInfo> AddImportMethod = new Lazy<MethodInfo>(() => typeof(FileDescriptorProto).GetMethod("AddImport", BindingFlags.NonPublic | BindingFlags.Instance));

        [Theory]
        [MemberData(nameof(Dependencies))]
        public async Task ShouldIncludeDependenciesInCorrectOrder(Type service, string symbolName, string[] expectedDescriptors, string[] expectedMessageTypes)
        {
            IServerReflection reflectionService = new ReflectionService(service);

            await foreach (var response in reflectionService.ServerReflectionInfoAsync(GetRequest()))
            {
                var fileDescriptors = response
                    .FileDescriptorResponse
                    .FileDescriptorProtoes
                    .Select(x => Serializer.Deserialize<FileDescriptorProto>(x.AsSpan()))
                    .ToArray();

                var fileDescriptorSet = new FileDescriptorSet();

                foreach (var fileDescriptor in fileDescriptors)
                {
                    // We need to add dependency as import, otherwise FileDescriptorSet.GetErrors() will return error about not finding imports.
                    foreach (var dependency in fileDescriptor.Dependencies)
                    {
                        // Use reflection.
                        var addImportMethod = AddImportMethod.Value;
                        addImportMethod.Invoke(fileDescriptor, new object[] {dependency, true, default});
                    }

                    fileDescriptorSet.Files.Add(fileDescriptor);
                }

                fileDescriptorSet.Process();

                Assert.Empty(fileDescriptorSet.GetErrors());
                Assert.Equal(expectedDescriptors, fileDescriptorSet.Files.Select(x => x.Name));
                Assert.Equal(expectedMessageTypes, fileDescriptorSet.Files.SelectMany(x => x.MessageTypes).Select(x => $".{x.GetFile().Package}.{x.Name}").OrderBy(x => x));
            }

            async IAsyncEnumerable<ServerReflectionRequest> GetRequest()
            {
                yield return new ServerReflectionRequest
                {
                    FileContainingSymbol = symbolName
                };

                await Task.CompletedTask;
            }
        }

        public static IEnumerable<object[]> Dependencies => new[]
        {
            new object[]
            {
                typeof(BclService),
                ".ReflectionTest.BclService",
                new[]
                {
                    "google/protobuf/empty.proto",
                    "protobuf-net/bcl.proto",
                    "ReflectionTest.BclService.proto"
                }
                ,
                new[]
                {
                    ".bcl.DateTime",
                    ".bcl.Decimal",
                    ".bcl.Guid",
                    ".bcl.NetObjectProxy",
                    ".bcl.TimeSpan",
                    ".google.protobuf.Empty",
                    ".ReflectionTest.BclMessage",
                }
            },
            new object[]
            {
                typeof(ReflectionTest.Service.Nested),
                ".ReflectionTest.Service.Nested",
                new[]
                {
                    "ReflectionTest.Service.Nested.proto",
                },
                new[]
                {
                    ".ReflectionTest.Service.Four",
                    ".ReflectionTest.Service.One",
                    ".ReflectionTest.Service.Three",
                    ".ReflectionTest.Service.Two",
                }
            },
        };
    }
}

namespace ReflectionTest
{
    [ProtoContract]
    public class BclMessage
    {
        [ProtoMember(1)]
        [CompatibilityLevel(CompatibilityLevel.Level200)]
        public Guid Id { get; set; } = Guid.Empty;

        [ProtoMember(2)]
        [CompatibilityLevel(CompatibilityLevel.Level200)]
        public DateTime DateTime { get; set; } = DateTime.MinValue;

        [ProtoMember(3)]
        [CompatibilityLevel(CompatibilityLevel.Level200)]
        public TimeSpan TimeSpan { get; set; } = TimeSpan.MinValue;

        [ProtoMember(4)]
        [CompatibilityLevel(CompatibilityLevel.Level200)]
        public decimal Decimal { get; set; } = 0M;
    }

    [Service]
    public interface IBclService
    {
        ValueTask M(BclMessage request);
    }

    public class BclService : IBclService
    {
        public ValueTask M(BclMessage request) => throw new NotImplementedException();
    }
}

namespace ReflectionTest.One
{
    [ProtoContract]
    public class One
    {
        [ProtoMember(1)] public string P { get; set; } = string.Empty;
    }
}

namespace ReflectionTest.Two
{
    using One;

    [ProtoContract]
    public class Two
    {
        [ProtoMember(1)] public One P { get; set; } = new One();
    }
}

namespace ReflectionTest.Three
{
    using Two;

    [ProtoContract]
    public class Three
    {
        [ProtoMember(1)] public Two P { get; set; } = new Two();
    }
}

namespace ReflectionTest.Four
{
    using Three;

    [ProtoContract]
    public class Four
    {
        [ProtoMember(1)] public Three P { get; set; } = new Three();
    }
}

namespace ReflectionTest.Service
{
    using Three;
    using Four;

    [Service]
    public interface INested
    {
        ValueTask<Three> M(Four request);
    }

    public class Nested : INested
    {
        public ValueTask<Three> M(Four request) => throw new NotImplementedException();
    }
}
