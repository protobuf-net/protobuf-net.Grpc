using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;

namespace MegaCorp
{
    [Service]
    public interface ITimeService
    {
        [Operation]
        IAsyncEnumerable<TimeResult> SubscribeAsync(CallContext context = default);
    }

    [ProtoContract]
    public class TimeResult
    {
        [ProtoMember(1, DataFormat = DataFormat.WellKnown)]
        public DateTime Time { get; set; }

        [ProtoMember(2)]
        public Guid Id { get; set; }
    }
}
