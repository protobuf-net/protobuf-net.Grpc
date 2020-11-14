using System;
using System.Runtime.Serialization;

namespace TraderSys.Portfolios.Models
{
    [DataContract]
    public class GetAllPortfoliosRequest
    {
        [DataMember(Order = 1)]
        public Guid TraderId { get; set; }
    }
}
