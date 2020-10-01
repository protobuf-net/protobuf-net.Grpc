using System;

namespace TraderSys.StockMarket
{
    public class StockPriceUpdateEventArgs : EventArgs
    {
        public StockPriceUpdateEventArgs(string symbol, decimal price)
        {
            Symbol = symbol;
            Price = price;
        }

        public string Symbol { get; }
        public decimal Price { get; }
    }

}
