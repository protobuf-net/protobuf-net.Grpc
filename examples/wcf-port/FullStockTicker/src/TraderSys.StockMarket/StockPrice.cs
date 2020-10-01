using System;

namespace TraderSys.StockMarket
{
    internal class StockPrice : IEquatable<StockPrice>
    {
        public StockPrice(string symbol, decimal price)
        {
            Symbol = symbol;
            Price = price;
        }

        public string Symbol { get; }
        public decimal Price { get; set; }

        public bool Equals(StockPrice other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Symbol, other.Symbol, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StockPrice) obj);
        }

        public override int GetHashCode()
        {
            return (Symbol != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Symbol) : 0);
        }

        public static bool operator ==(StockPrice left, StockPrice right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StockPrice left, StockPrice right)
        {
            return !Equals(left, right);
        }
    }
}