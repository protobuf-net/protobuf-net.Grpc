using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using protobuf_net.Grpc.Reflection.Test;
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

        public interface INotAService
        {
            ValueTask<MyResponse> SomeMethod1(MyRequest request, CallContext callContext = default);
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
    