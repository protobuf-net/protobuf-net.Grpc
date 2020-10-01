using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;
using TraderSys.SimpleStockTickerServer.Shared;
using TraderSys.StockMarket;

namespace TraderSys.SimpleStockTickerServer.Services
{
    public class StockTickerService : IStockTickerService, IDisposable
    {
        private readonly IStockPriceSubscriberFactory _subscriberFactory;
        private readonly ILogger<StockTickerService> _logger;
        private IStockPriceSubscriber _subscriber;

        public StockTickerService(IStockPriceSubscriberFactory subscriberFactory, ILogger<StockTickerService> logger)
        {
            _subscriberFactory = subscriberFactory;
            _logger = logger;
        }

        public IAsyncEnumerable<StockTickerUpdate> Subscribe(SubscribeRequest request, CallContext context = default)
        {
            var buffer = Channel.CreateUnbounded<StockTickerUpdate>();

            _subscriber = _subscriberFactory.GetSubscriber(request.Symbols.ToArray());
            _subscriber.Update += async (sender, args) =>
            {
                try
                {
                    await buffer.Writer.WriteAsync(new StockTickerUpdate
                    {
                        Symbol = args.Symbol,
                        Price = args.Price,
                        Time = DateTime.UtcNow
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to write message: {e.Message}");
                }
            };

            return buffer.AsAsyncEnumerable(context.CancellationToken);
        }

        public void Dispose()
        {
            _subscriber?.Dispose();
        }
    }
}