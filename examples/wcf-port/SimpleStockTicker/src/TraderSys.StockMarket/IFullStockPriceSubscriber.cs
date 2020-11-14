using System;

namespace TraderSys.StockMarket
{
    public interface IFullStockPriceSubscriber : IDisposable
    {
        event EventHandler<StockPriceUpdateEventArgs> Update;

        void Add(string symbol);
        void Remove(string symbol);
    }


}