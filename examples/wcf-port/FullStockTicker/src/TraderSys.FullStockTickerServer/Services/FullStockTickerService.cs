using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;
using TraderSys.FullStockTicker.Shared;
using TraderSys.StockMarket;

namespace TraderSys.FullStockTickerServer.Services
{
    public class FullStockTickerService : IFullStockTicker, IDisposable
    {
        private readonly IFullStockPriceSubscriberFactory _subscriberFactory;
        private readonly ILogger<FullStockTickerService> _logger;
        private IFullStockPriceSubscriber _subscriber;
        private Task _processRequestTask;
        private CancellationTokenSource _cts;

        public FullStockTickerService(IFullStockPriceSubscriberFactory subscriberFactory, ILogger<FullStockTickerService> logger)
        {
            _subscriberFactory = subscriberFactory;
            _logger = logger;
            _cts = new CancellationTokenSource();
        }

        public IAsyncEnumerable<StockTickerUpdate> Subscribe(IAsyncEnumerable<SymbolRequest> request, CallContext context)
        {
            var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, context.CancellationToken).Token;
            var buffer = Channel.CreateUnbounded<StockTickerUpdate>();

            _subscriber = _subscriberFactory.GetSubscriber();
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

            _processRequestTask = ProcessRequests(request, buffer.Writer, cancellationToken);

            return buffer.AsAsyncEnumerable(cancellationToken);
        }

        private async Task ProcessRequests(IAsyncEnumerable<SymbolRequest> requests, ChannelWriter<StockTickerUpdate> writer, CancellationToken cancellationToken)
        {
            await foreach (var request in requests.WithCancellation(cancellationToken))
            {
                switch (request.Action)
                {
                    case SymbolRequestAction.Add:
                        _subscriber.Add(request.Symbol);
                        break;
                    case SymbolRequestAction.Remove:
                        _subscriber.Remove(request.Symbol);
                        break;
                    default:
                        _logger.LogWarning($"Unknown Action '{request.Action}'.");
                        break;
                }
            }

            writer.Complete();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _subscriber?.Dispose();
        }
    }
}
