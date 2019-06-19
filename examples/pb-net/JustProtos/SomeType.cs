using Google.Protobuf.Reflection;
using Hyper;
using MegaCorp;
using System;

namespace JustProtos
{
    class SomeType
    {
        void Foo()
        {
            Type[] types = {
                typeof(DescriptorProto),
                typeof(TimeResult),
                typeof(MultiplyRequest),
            };
        }
    }
}
