using System.Threading.Tasks;
using System.Windows.Input;

namespace TraderSys.FullStockTickerClientApp
{
    public interface IAsyncCommand : ICommand
    {
        bool CanExecute();
        Task ExecuteAsync();
        void RaiseCanExecuteChanged();
    }
}