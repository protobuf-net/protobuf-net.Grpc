namespace TraderSys.StockMarket
{
    public class FullStockPriceSubscriberFactory : IFullStockPriceSubscriberFactory
    {
        public IFullStockPriceSubscriber GetSubscriber()
        {
            return new FullStockPriceSubscriber();
        }
    }
}