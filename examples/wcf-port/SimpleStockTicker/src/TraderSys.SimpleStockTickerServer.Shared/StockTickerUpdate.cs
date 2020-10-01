using ProtoBuf.WellKnownTypes;
using System;
using System.Runtime.Serialization;

namespace TraderSys.SimpleStockTickerServer.Shared
{
    [DataContract]
    public class StockTickerUpdate
    {
        [DataMember(Order = 1)]
        public string Symbol { get; set; }

        [DataMember(Order = 2)]
        public decimal Price { get; set; }

        [DataMember(Order = 3)]
        public DateTime Time { get; set; }
    }
}