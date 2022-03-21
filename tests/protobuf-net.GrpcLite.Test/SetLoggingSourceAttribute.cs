using ProtoBuf.Grpc.Lite;
using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.Reflection;
using Xunit.Sdk;

namespace protobuf_net.GrpcLite.Test;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class SetLoggingSourceAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest)
        => Logging.SetSource(null, LogKind.Client, "test " + methodUnderTest.Name);
}
