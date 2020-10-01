using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TraderSys.SimpleStockTickerServer.Shared
{
    [DataContract]
    public class SubscribeRequest
    {
        [DataMember(Order = 1)]
        public List<string> Symbols { get; set; } = new List<string>();
    }
}