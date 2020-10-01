using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TraderSys.Portfolios.Models
{
    [DataContract]
    public class PortfolioCollection
    {
        [DataMember(Order = 1)]
        public List<Portfolio> Items { get; set; }
    }

    [DataContract]
    public class Portfolio
    {
        [DataMember(Order = 1)]
        public int Id { get; set; }

        [DataMember(Order = 2)]
        public Guid TraderId { get; set; }

        [DataMember(Order = 3)]
        public List<PortfolioItem> Items { get; set; }
    }
}
