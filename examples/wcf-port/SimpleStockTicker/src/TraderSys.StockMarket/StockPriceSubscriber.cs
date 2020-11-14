using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TraderSys.StockMarket
{
    public class StockPriceSubscriber : IStockPriceSubscriber
    {
        private readonly StockPrice[] _prices;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _task;
        private readonly Random _random;

        public StockPriceSubscriber(string[] symbols)
        {
            _random = new Random(42);
            _prices = symbols.Select(s => new StockPrice(s, _random.Next(99999) / 10m)).ToArray();
            _cancellationTokenSource = new CancellationTokenSource();
            _task = RunAsync(_cancellationTokenSource.Token);
        }

        public event EventHandler<StockPriceUpdateEventArgs> Update;

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(_random.Next(5000), token);
                    if (Update != null)
                    {
                        var price = _prices[_random.Next(_prices.Length)];
                        var newPrice = price.Price + _random.Next(-99, 99) / 10m;
                        if (newPrice < 0) newPrice = 0m;
                        price.Price = newPrice;
                        Update?.Invoke(this, new StockPriceUpdateEventArgs(price.Symbol, price.Price));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }
    }


}