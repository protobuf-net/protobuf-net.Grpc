using System;
using System.Runtime.Serialization;

namespace TraderSys.Portfolios.Models
{
    [DataContract]
    public class GetPortfolioRequest
    {
        [DataMember(Order = 1)]
        public Guid TraderId { get; set; }

        [DataMember(Order = 2)]
        public int PortfolioId { get; set; }
    }
}
