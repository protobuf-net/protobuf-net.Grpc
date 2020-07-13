using Google.Protobuf.Reflection;
using Hyper;
using MegaCorp;
using System;

namespace JustProtos
{
#pragma warning disable IDE0051, IDE0059 // unused and unloved
    internal static class SomeType
    {
        static void Foo()
        {
            // the point here being: these types *exist*, despite
            // not appearing as local .cs files
            Type[] types = {
                typeof(DescriptorProto),
                typeof(TimeResult),
                typeof(MultiplyRequest),
            };
            _ = types;
        }
    }
#pragma warning restore IDE0051, IDE0059
}
