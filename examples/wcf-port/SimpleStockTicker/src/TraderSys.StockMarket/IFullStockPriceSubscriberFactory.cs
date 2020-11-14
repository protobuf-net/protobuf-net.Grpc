namespace TraderSys.StockMarket
{
    public interface IFullStockPriceSubscriberFactory
    {
        IFullStockPriceSubscriber GetSubscriber();
    }

}