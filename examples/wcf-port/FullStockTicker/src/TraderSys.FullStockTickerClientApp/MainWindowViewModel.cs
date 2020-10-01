using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc;
using TraderSys.FullStockTicker.Shared;
using TraderSys.FullStockTickerClientApp.Annotations;

namespace TraderSys.FullStockTickerClientApp
{
    public class MainWindowViewModel : IAsyncDisposable, INotifyPropertyChanged
    {
        private readonly IFullStockTicker _client;
        private readonly Channel<SymbolRequest> _requestStream;
        private readonly IAsyncEnumerable<StockTickerUpdate> _responseStream;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _responseTask;
        private string _addSymbol;

        public MainWindowViewModel(IFullStockTicker client)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _client = client;
            _requestStream = Channel.CreateUnbounded<SymbolRequest>();
            _responseStream = _client.Subscribe(_requestStream.Reader.AsAsyncEnumerable());
            _responseTask = HandleResponsesAsync(_cancellationTokenSource.Token);
            
            AddCommand = new AsyncCommand(Add, CanAdd);
        }
        
        public IAsyncCommand AddCommand { get; }

        public string AddSymbol
        {
            get => _addSymbol;
            set
            {
                if (value == _addSymbol) return;
                _addSymbol = value;
                AddCommand.RaiseCanExecuteChanged();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PriceViewModel> Prices { get; } = new ObservableCollection<PriceViewModel>();

        private bool CanAdd() => !string.IsNullOrWhiteSpace(AddSymbol);

        private async Task Add()
        {
            if (CanAdd())
            {
                await _requestStream.Writer.WriteAsync(new SymbolRequest { Action = SymbolRequestAction.Add, Symbol = AddSymbol });
            }
        }

        public async Task Remove(PriceViewModel priceViewModel)
        {
            await _requestStream.Writer.WriteAsync(new SymbolRequest { Action = SymbolRequestAction.Remove, Symbol = priceViewModel.Symbol });
            Prices.Remove(priceViewModel);
        }

        private async Task HandleResponsesAsync(CancellationToken token)
        {
            await foreach(var update in _responseStream.WithCancellation(token))
            {
                var price = Prices.FirstOrDefault(p => p.Symbol.Equals(update.Symbol));
                if (price == null)
                {
                    price = new PriceViewModel(this) {Symbol = update.Symbol, Price = Convert.ToDecimal(update.Price)};
                    Prices.Add(price);
                }
                else
                {
                    price.Price = Convert.ToDecimal(update.Price);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _requestStream.Writer.Complete();
                await _responseTask.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // noop
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}