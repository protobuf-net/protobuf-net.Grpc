namespace TraderSys.StockMarket
{
    public interface IStockPriceSubscriberFactory
    {
        IStockPriceSubscriber GetSubscriber(string[] symbols);
    }

}