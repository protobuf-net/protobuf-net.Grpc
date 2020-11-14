using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using TraderSys.FullStockTickerClientApp.Annotations;

namespace TraderSys.FullStockTickerClientApp
{
    public sealed class PriceViewModel : INotifyPropertyChanged
    {
        private decimal _price;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public PriceViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
            RemoveCommand = new AsyncCommand(Remove);
        }

        public string Symbol { get; set; }
        public ICommand RemoveCommand { get; }

        private Task Remove() => _mainWindowViewModel.Remove(this);

        public decimal Price
        {
            get => _price;
            set
            {
                if (value == _price) return;
                _price = value;
                OnPropertyChanged();
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