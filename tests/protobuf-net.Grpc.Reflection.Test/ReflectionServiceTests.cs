using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Reflection;
using grpc.reflection.v1alpha;
using ProtoBuf;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection;
using Xunit;

namespace protobuf_net.Grpc.Reflection.Test
{
    using ReflectionTest;

    public class ReflectionServiceTests
    {
        [Theory]
        [MemberData(nameof(Dependencies))]
        public async Task ShouldIncludeDependenciesInCorrectOrder(Type service, string symbolName, string[] expectedDescriptors)
        {
            IServerReflection reflectionService = new ReflectionService(service);

            await foreach (var response in reflectionService.ServerReflectionInfoAsync(GetRequest()))
            {
                var fileDescriptors = response
                    .FileDescriptorResponse
                    .FileDescriptorProtoes
                    .Select(x => Serializer.Deserialize<FileDescriptorProto>(x.AsSpan()))
                    .ToArray();

                Assert.Equal(expectedDescriptors, fileDescriptors.Select(x => x.Name));
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
                    "ProtoBuf.Grpc.Internal.Empty.proto", // TODO: Maybe Google well-known should have correct name?
                    "protobuf-net/bcl.proto",
                    "ReflectionTest.BclMessage.proto",
                    "ReflectionTest.BclService.proto"
                }
            },
            new object[]
            {
                typeof(Nested),
                ".ReflectionTest.Nested",
                new[]
                {
                    "ReflectionTest.Three.proto", // This includes Two and One
                    "ReflectionTest.Four.proto", // This includes Three, Two and One
                    "ReflectionTest.Nested.proto",
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

namespace ReflectionTest
{
    [ProtoContract]
    public class One
    {
        [ProtoMember(1)] public string P { get; set; } = string.Empty;
    }

    [ProtoContract]
    public class Two
    {
        [ProtoMember(1)] public One P { get; set; } = new One();
    }

    [ProtoContract]
    public class Three
    {
        [ProtoMember(1)] public Two P { get; set; } = new Two();
    }

    [ProtoContract]
    public class Four
    {
        [ProtoMember(1)] public Three P { get; set; } = new Three();
    }

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
