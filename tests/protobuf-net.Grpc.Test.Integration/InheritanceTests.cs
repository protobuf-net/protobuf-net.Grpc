using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;
using ProtoBuf.Meta;
using Xunit;
using Xunit.Abstractions;

namespace ProtobufNet.Grpc.Test.Integration
{
    [ProtoContract]
    [ProtoInclude(10, typeof(Sub))]
    [ProtoInclude(11, typeof(GenericBase<>))]
    public abstract class Base
    {
        [ProtoMember(1)]
        public string String { get; set; }
        [ProtoMember(2)]
        public Dictionary<string,string> StringDictionary { get; set; }
    }

    [ProtoContract]
    public class Sub : Base
    {
        [ProtoMember(1)]
        public long Long { get; set; }
    }

    [ProtoContract]
    public class Poco
    {
        [ProtoMember(1)]
        public short Short { get; set; }
    }

    [ProtoContract]
    [ProtoInclude(20, typeof(SubGeneric))]
    public class GenericBase<T> : Base
    {
        [ProtoMember(1)]
        public T Result { get; set; }
    }

    [ProtoContract]
    public class SubGeneric : GenericBase<Poco>
    {
        [ProtoMember(1)]
        public int Int { get; set; }
    }
    
    public class InheritanceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        public InheritanceTests(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

        [Fact]
        public void CanDeserializeInheritedTypesWithProtobufnet()
        {
            var from = new Sub { 
                Long = 1, 
                String = "Base", 
                StringDictionary = new Dictionary<string, string> {
                    {"A", "OK"}
                }
            };

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, from);
            ms.Position = 0;
            var to = Serializer.Deserialize<Sub>(ms);
            
            Assert.Equal(from.Long, to.Long);
            Assert.Equal(from.String, to.String);
            Assert.Equal(from.StringDictionary["A"], to.StringDictionary["A"]);
        }

        [Fact]
        public void CanDeserializeInheritedGenericTypesWithProtobufnet()
        {
            var from = new SubGeneric { 
                Int = 1, 
                String = "Base", 
                StringDictionary = new Dictionary<string, string> {
                    {"A", "OK"}
                },
                Result = new Poco {
                    Short = 2
                }
            };

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, from);
            ms.Position = 0;
            var to = Serializer.Deserialize<SubGeneric>(ms);
            
            Assert.Equal(from.Result.Short, to.Result.Short);
            Assert.Equal(from.Int, to.Int);
            Assert.Equal(from.String, to.String); //FAIL to.String == null
            Assert.Equal(from.StringDictionary["A"], to.StringDictionary["A"]); //FAIL to.StringDictionary == null
        }

        [Fact]
        public void CanGenerateSchemaWithInheritedTypes()
        {
            var typeModel  = TypeModel.Create();
            foreach (var type in new[] { typeof(Base), typeof(Sub), typeof(GenericBase<>), typeof(SubGeneric), typeof(Poco) })
            {
                var _ = typeModel[type];
            }

            var schema = typeModel.GetSchema(null, ProtoSyntax.Proto3);
            _testOutputHelper.WriteLine(schema);
            Assert.Equal(@"
syntax = ""proto3"";
package protobuf_net.Grpc.Test.Integration;

message Base {
   string String = 1;
   map<string,string> StringDictionary = 2;
   oneof subtype {
      Sub Sub = 10;
      GenericBase GenericBase = 11;
   }
}
message GenericBase {
   T Result = 1;
}
message GenericBase_Poco {
   Poco Result = 1;
   oneof subtype {
      SubGeneric SubGeneric = 20;
   }
}
message Poco {
   int32 Short = 1;
}
message Sub {
   int64 Long = 1;
}
message SubGeneric {
   int32 Int = 1;
}
message T {
}".NormalizeNewLines(), schema.NormalizeNewLines());
        }
    }
}