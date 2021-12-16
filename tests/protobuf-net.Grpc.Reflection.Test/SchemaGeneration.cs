using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using protobuf_net.Grpc.Reflection.Test;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Xunit;
using Xunit.Abstractions;

[module: CompatibilityLevel(CompatibilityLevel.Level300)] // configures how DateTime etc are handled

namespace protobuf_net.Grpc.Reflection.Test
{
    public class SchemaGeneration
    {
        public SchemaGeneration(ITestOutputHelper log) => _log = log;
        private readonly ITestOutputHelper _log;
        private void Log(string message) => _log?.WriteLine(message);
        [Fact]
        public void CheckBasicSchema()
        {
            var generator = new SchemaGenerator();
            var schema = generator.GetSchema<IMyService>();
            Log(schema);
            Assert.Equal(@"syntax = ""proto3"";
package protobuf_net.Grpc.Reflection.Test;
import ""google/protobuf/empty.proto"";
import ""google/protobuf/timestamp.proto"";

enum Category {
   Default = 0;
   Foo = 1;
   Bar = 2;
}
message MyRequest {
   int32 Id = 1;
   .google.protobuf.Timestamp When = 2;
}
message MyResponse {
   string Value = 1;
   Category Category = 2;
   string RefId = 3; // default value could not be applied: 00000000-0000-0000-0000-000000000000
}
service MyService {
   rpc AsyncEmpty (.google.protobuf.Empty) returns (.google.protobuf.Empty);
   rpc ClientStreaming (stream MyRequest) returns (MyResponse);
   rpc FullDuplex (stream MyRequest) returns (stream MyResponse);
   rpc ServerStreaming (MyRequest) returns (stream MyResponse);
   rpc SyncEmpty (.google.protobuf.Empty) returns (.google.protobuf.Empty);
   rpc Unary (MyRequest) returns (MyResponse);
}
", schema, ignoreLineEndingDifferences: true);
        }
        
        [Fact]
        public void CheckInheritedInterfaceSchema()
        {
            var generator = new SchemaGenerator();
            var schema = generator.GetSchema<IMyInheritedService>();
            Log(schema);
            Assert.Equal(@"syntax = ""proto3"";
package protobuf_net.Grpc.Reflection.Test;
import ""google/protobuf/empty.proto"";
import ""google/protobuf/timestamp.proto"";

enum Category {
   Default = 0;
   Foo = 1;
   Bar = 2;
}
message MyRequest {
   int32 Id = 1;
   .google.protobuf.Timestamp When = 2;
}
message MyResponse {
   string Value = 1;
   Category Category = 2;
   string RefId = 3; // default value could not be applied: 00000000-0000-0000-0000-000000000000
}
service MyInheritedService {
   rpc GenericUnary (MyRequest) returns (MyResponse);
   rpc InheritedAsyncEmpty (.google.protobuf.Empty) returns (.google.protobuf.Empty);
   rpc InheritedClientStreaming (stream MyRequest) returns (MyResponse);
   rpc InheritedFullDuplex (stream MyRequest) returns (stream MyResponse);
   rpc InheritedServerStreaming (MyRequest) returns (stream MyResponse);
   rpc InheritedSyncEmpty (.google.protobuf.Empty) returns (.google.protobuf.Empty);
   rpc InheritedUnary (MyRequest) returns (MyResponse);
}
", schema, ignoreLineEndingDifferences: true);
        }

        [Service]
        public interface IMyService
        {
            ValueTask<MyResponse> Unary(MyRequest request, CallContext callContext = default);
            ValueTask<MyResponse> ClientStreaming(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> ServerStreaming(MyRequest request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> FullDuplex(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);

            ValueTask AsyncEmpty();
            void SyncEmpty();
        }

        /// <summary>
        /// An interface which is not marked with [Service] attribute.
        /// Its methods are not expected to participate in reflection at all.
        /// </summary>
        public interface INotAService
        {
            ValueTask<MyResponse> NotAServiceUnary(MyRequest request, CallContext callContext = default);
            ValueTask<MyResponse> NotAServiceClientStreaming(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> NotAServiceServerStreaming(MyRequest request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> NotAServiceFullDuplex(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);

            ValueTask NotAServiceAsyncEmpty();
            void NotAServiceSyncEmpty();
        }
        
        [Theory]
        [InlineData(typeof(IMyService))]
        [InlineData(typeof(IMyInheritedService))]        
        [InlineData(typeof(IMyServiceInheritTwoLevelsOfHierarchy))]        
        public void CompareRouteTable(Type type)
        {
            // 1: use the existing binder logic to build the routes, using the server logic
            var binder = new TestBinder();
            binder.Bind(type, type, BinderConfiguration.Default);
            var viaBinder = binder.Collect();
            foreach (var method in viaBinder) Log(method);
            Log("");


            // 2: create a schema and parse it for equivalece
            var generator = new SchemaGenerator();
            var schema = generator.GetSchema(type);
            Log(schema);
            var fds = new FileDescriptorSet();
            fds.Add("my.proto", source: new StringReader(schema));
            fds.Process();
            Assert.Empty(fds.GetErrors());
            var viaSchema = new List<string>(viaBinder.Length);
            var file = fds.Files.Single(static x => x.IncludeInOutput);
            foreach (var service in file.Services)
            {
                var svcName = string.IsNullOrEmpty(file.Package) ? service.Name : $"{file.Package}.{service.Name}";
                foreach (var method in service.Methods)
                {
                    viaSchema.Add($"/{svcName}/{method.Name}");
                }
            }
            viaSchema.Sort();

            var routeTableViaBinder = string.Join(Environment.NewLine, viaBinder);
            var routeTableViaSchame = string.Join(Environment.NewLine, viaSchema);
            Assert.Equal(routeTableViaBinder, routeTableViaSchame);

        }

        class TestBinder : ServerBinder
        {
            private readonly List<string> _methods = new List<string>();
            protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
            {
                _methods.Add(method.FullName);
                return true;
            }

            public string[] Collect()
            {
                _methods.Sort();
                var arr = _methods.ToArray();
                _methods.Clear(); // reset
                return arr;
            }
        }
        
        [ServiceInheritable]
        public interface ISomeInheritableGenericService<in TGenericRequest, TGenericResult>
        {
            ValueTask<TGenericResult> GenericUnary(TGenericRequest request, CallContext callContext = default);
        }
        
        [Service]
        public interface IMyInheritedService : ISomeInheritableGenericService<MyRequest, MyResponse>, INotAService
        {
            ValueTask<MyResponse> InheritedUnary(MyRequest request, CallContext callContext = default);
            ValueTask<MyResponse> InheritedClientStreaming(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> InheritedServerStreaming(MyRequest request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> InheritedFullDuplex(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);

            ValueTask InheritedAsyncEmpty();
            void InheritedSyncEmpty();
        }
        
        [ServiceInheritable]
        public interface ISecondLevelInheritable : ISomeInheritableGenericService<MyRequest, MyResponse>, INotAService
        {
            ValueTask<MyResponse> InheritedUnary(MyRequest request, CallContext callContext = default);
            ValueTask<MyResponse> InheritedClientStreaming(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> InheritedServerStreaming(MyRequest request, CallContext callContext = default);
            IAsyncEnumerable<MyResponse> InheritedFullDuplex(IAsyncEnumerable<MyRequest> request, CallContext callContext = default);

            ValueTask InheritedAsyncEmpty();
            void InheritedSyncEmpty();
        }

        [Service]
        public interface IMyServiceInheritTwoLevelsOfHierarchy : ISecondLevelInheritable
        {
            ValueTask<MyResponse> AnotherMethod(MyRequest request, CallContext callContext = default);
        }

        [DataContract]
        public class MyRequest
        {
            [DataMember(Order = 1)]
            public int Id { get; set; }

            // with protobuf-net v3, this could use DataContract/DataMember throughput, and
            // just use CompatibilityLevel 300+
            [DataMember(Order = 2)]
            public DateTime When { get; set; }
        }

        [DataContract]
        public class MyResponse
        {
            [DataMember(Order = 1)]
            public string? Value { get; set; }

            [DataMember(Order = 2)]
            public Category Category { get; set; }

            [DataMember(Order = 3)]
            public Guid RefId { get; set; }
        }

        public enum Category
        {
            Default = 0,
            Foo = 1,
            Bar = 2,
        }

        [Service]
        public interface IConferencesService
        {
            Task<IEnumerable<ConferenceOverview>> ListConferencesEnumerable();
            IAsyncEnumerable<ConferenceOverview> ListConferencesAsyncEnumerable();
            Task<ListConferencesResult> ListConferencesWrapped();
        }

        [DataContract]
        public class ConferenceOverview
        {
            [DataMember(Order = 1)]
            public Guid ID { get; set; }

            [DataMember(Order = 2)]
            public string? Title { get; set; }
        }

        [DataContract]
        public class ListConferencesResult
        {
            [ProtoMember(1)]
            public List<ConferenceOverview> Conferences { get; } = new List<ConferenceOverview>();
        }

        [Fact]
        public void ConferenceServiceSchema()
        {
            var generator = new SchemaGenerator();
            var proto = generator.GetSchema<IConferencesService>();
            Log(proto);
            Assert.Equal(@"syntax = ""proto3"";
package protobuf_net.Grpc.Reflection.Test;
import ""google/protobuf/empty.proto"";

message ConferenceOverview {
   string ID = 1; // default value could not be applied: 00000000-0000-0000-0000-000000000000
   string Title = 2;
}
message IEnumerable_ConferenceOverview {
   repeated ConferenceOverview items = 1;
}
message ListConferencesResult {
   repeated ConferenceOverview Conferences = 1;
}
service ConferencesService {
   rpc ListConferencesAsyncEnumerable (.google.protobuf.Empty) returns (stream ConferenceOverview);
   rpc ListConferencesEnumerable (.google.protobuf.Empty) returns (IEnumerable_ConferenceOverview);
   rpc ListConferencesWrapped (.google.protobuf.Empty) returns (ListConferencesResult);
}
", proto, ignoreLineEndingDifferences: true);
        }
        
        [Fact]
        public void WhenInterfaceIsNotServiceContract_Throw()
        {
            var generator = new SchemaGenerator();
            Action activation = () => generator.GetSchema<INotAService>();
            Assert.Throws<ArgumentException>(activation.Invoke);
        }
        
        // ReSharper disable once ClassNeverInstantiated.Global
        public class NotAService
        {
            public Task<MyResponse> SomeMethod1(MyRequest request, CallContext callContext = default) 
                => Task.FromResult(new MyResponse());
        }        
        [Fact]
        public void WhenClassIsNotServiceContract_Throw()
        {
            var generator = new SchemaGenerator();
            Action activation = () => generator.GetSchema<NotAService>();
            Assert.Throws<ArgumentException>(activation.Invoke);
        }

        [Service]
        public interface ISimpleService1
        {
            ValueTask<MyResponse> SomeMethod1(MyRequest request, CallContext callContext = default);
        }
        [Service]
        public interface ISimpleService2
        {
            ValueTask<MyResponse> SomeMethod2(MyRequest request, CallContext callContext = default);
        }
        
        /// <summary>
        /// When we have multiple services which share same classes,
        /// we would like to have a schema which defines those  services
        /// while having their shared classes defined only once - in that schema.
        /// The proto schema consumer will generate code (in any language) while the classes are common to the several services
        /// and can be reused towards multiple services, the same way the code-first uses those shared classes.. 
        /// </summary>
        [Fact]
        public void MultiServicesInSameSchema_ServicesAreFromSameNamespace_Success()
        {
            var generator = new SchemaGenerator();
            
            var proto =  generator.GetSchema(typeof(ISimpleService1), typeof(ISimpleService2));
            
            Assert.Equal(@"syntax = ""proto3"";
package protobuf_net.Grpc.Reflection.Test;
import ""google/protobuf/timestamp.proto"";

enum Category {
   Default = 0;
   Foo = 1;
   Bar = 2;
}
message MyRequest {
   int32 Id = 1;
   .google.protobuf.Timestamp When = 2;
}
message MyResponse {
   string Value = 1;
   Category Category = 2;
   string RefId = 3; // default value could not be applied: 00000000-0000-0000-0000-000000000000
}
service SimpleService1 {
   rpc SomeMethod1 (MyRequest) returns (MyResponse);
}
service SimpleService2 {
   rpc SomeMethod2 (MyRequest) returns (MyResponse);
}
", proto, ignoreLineEndingDifferences: true);            
        }
        
        
        /// <summary>
        /// When we have multiple services but with different namespaces,
        /// since schema should export a single package, this situation is unsupported.
        /// We expect for an exception.
        /// </summary>
        [Fact]
        public void MultiServicesInSameSchema_ServicesAreFromDifferentNamespaces_Throw()
        {
            var generator = new SchemaGenerator();
            
            Action activation = () => generator.GetSchema(typeof(DifferentNamespace1.IServiceInNamespace1), typeof(DifferentNamespace2.IServiceInNamespace2));
            Assert.Throws<ArgumentException>(activation.Invoke);                  
        }
    }
}


namespace DifferentNamespace1
{

    [Service]
    public interface IServiceInNamespace1
    {
        ValueTask<SchemaGeneration.MyResponse> SomeMethod1(SchemaGeneration.MyRequest request, CallContext callContext = default);
    }
}
namespace DifferentNamespace2
{
    [Service]
    public interface IServiceInNamespace2
    {
        ValueTask<SchemaGeneration.MyResponse> SomeMethod2(SchemaGeneration.MyRequest request, CallContext callContext = default);
    }

}
