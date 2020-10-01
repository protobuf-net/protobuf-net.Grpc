using System;

namespace TraderSys.StockMarket
{
    public interface IStockPriceSubscriber : IDisposable
    {
        event EventHandler<StockPriceUpdateEventArgs> Update;
    }


}