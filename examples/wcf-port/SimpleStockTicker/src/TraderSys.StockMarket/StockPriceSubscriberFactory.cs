namespace TraderSys.StockMarket
{
    public class StockPriceSubscriberFactory : IStockPriceSubscriberFactory
    {
        public IStockPriceSubscriber GetSubscriber(string[] symbols)
        {
            return new StockPriceSubscriber(symbols);
        }
    }

    public class FullStockPriceSubscriberFactory : IFullStockPriceSubscriberFactory
    {
        public IFullStockPriceSubscriber GetSubscriber()
        {
            return new FullStockPriceSubscriber();
        }
    }
}