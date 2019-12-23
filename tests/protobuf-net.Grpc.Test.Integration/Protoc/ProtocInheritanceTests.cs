using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using ProtoBuf;
using Xunit;

namespace ProtobufNet.Grpc.Test.Integration.Protoc
{
    public class ProtocInheritanceTests
    {
        [Fact]
        public void CanDeserializeInheritedTypesWithProtoc()
        {
            var from = new Sub {
                Long = 1,
                // Cannot populate inherited properties: 
                // String = "Base", 
                // StringDictionary = new Dictionary<string, string> {
                //     {"A", "OK"}
                // }
            };

            using var ms = new MemoryStream();
            from.WriteTo(ms);
            ms.Position = 0;

            var to = Sub.Parser.ParseFrom(ms);

            Assert.Equal(from.Long, to.Long);
        }
        
        [Fact]
        public void CanDeserializeProtocInheritedTypesWithProtobufNet()
        {
            var from = new Sub { 
                Long = 1,
                // Cannot populate inherited properties: 
                // String = "Base", 
                // StringDictionary = new Dictionary<string, string> {
                //     {"A", "OK"}
                // }
            };
            
            using var ms = new MemoryStream();
            from.WriteTo(ms);
            ms.Position = 0;
            //throws System.InvalidOperationException : It was not possible to prepare a serializer for: ProtobufNet.Grpc.Test.Integration.Base
            var to = Serializer.Deserialize<Integration.Sub>(ms); 
            
            Assert.Equal(from.Long, to.Long);
            // Cannot populate inherited properties: 
            // Assert.Equal(from.String, to.String);
            // Assert.Equal(from.StringDictionary["A"], to.StringDictionary["A"]);
        }

    }
}