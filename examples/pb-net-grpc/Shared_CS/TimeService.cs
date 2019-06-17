using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.ServiceModel;
#pragma warning disable CS0618
namespace MegaCorp
{
    [ServiceContract]
    public interface ITimeService
    {
        IAsyncEnumerable<TimeResult> SubscribeAsync(Empty empty, CallContext context = default);
    }

    [ProtoContract]
    public class TimeResult
    {
        [ProtoMember(1, DataFormat = DataFormat.WellKnown)]
        public DateTime Time { get; set; }
    }
}
