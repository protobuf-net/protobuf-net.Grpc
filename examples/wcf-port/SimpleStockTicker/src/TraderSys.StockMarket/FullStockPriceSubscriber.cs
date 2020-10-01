using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TraderSys.StockMarket
{
    public class FullStockPriceSubscriber : IDisposable, IFullStockPriceSubscriber
    {
        private readonly HashSet<StockPrice> _prices = new HashSet<StockPrice>();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _task;
        private readonly Random _random;

        public FullStockPriceSubscriber()
        {
            _random = new Random(42);
            _cancellationTokenSource = new CancellationTokenSource();
            _task = RunAsync(_cancellationTokenSource.Token);
        }

        public event EventHandler<StockPriceUpdateEventArgs> Update;

        public void Add(string symbol)
        {
            _prices.Add(new StockPrice(symbol, _random.Next(99999) / 10m));
        }

        public void Remove(string symbol)
        {
            _prices.RemoveWhere(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(_random.Next(1024, 8192), token);
                    if (Update == null || _prices.Count == 0) continue;

                    var price = _prices.Skip(_random.Next(_prices.Count)).First();

                    var newPrice = price.Price + _random.Next(-99, 99) / 10m;
                    if (newPrice < 0) newPrice = 0m;
                    price.Price = newPrice;

                    Update?.Invoke(this, new StockPriceUpdateEventArgs(price.Symbol, price.Price));
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }


}