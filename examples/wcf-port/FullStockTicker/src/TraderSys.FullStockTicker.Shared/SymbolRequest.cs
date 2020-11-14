using System;
using System.Runtime.Serialization;

namespace TraderSys.FullStockTicker.Shared
{
    public enum SymbolRequestAction
    {
        Add = 0,
        Remove = 1
    }

    [DataContract]
    public class SymbolRequest
    {
        [DataMember(Order = 1)]
        public SymbolRequestAction Action { get; set; }

        [DataMember(Order = 2)]
        public string Symbol { get; set; }
    }
}
