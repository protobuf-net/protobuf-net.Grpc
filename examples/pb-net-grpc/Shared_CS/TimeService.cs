using ProtoBuf;
using ProtoBuf.Grpc;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace MegaCorp
{
    [ServiceContract]
    public interface ITimeService
    {
        IAsyncEnumerable<TimeResult> SubscribeAsync(CallContext context = default);
    }

    [ProtoContract]
    public class TimeResult
    {
        [ProtoMember(1, DataFormat = DataFormat.WellKnown)]
        public DateTime Time { get; set; }
    }
}
