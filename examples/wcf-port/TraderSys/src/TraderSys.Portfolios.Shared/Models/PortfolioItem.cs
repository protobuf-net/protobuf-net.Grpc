using System.Runtime.Serialization;

namespace TraderSys.Portfolios.Models
{
    [DataContract]
    public class PortfolioItem
    {
        [DataMember(Order = 1)]
        public int Id { get; set; }

        [DataMember(Order = 2)]
        public int ShareId { get; set; }

        [DataMember(Order = 3)]
        public int Holding { get; set; }

        [DataMember(Order = 4)]
        public decimal Cost { get; set; }
    }
}