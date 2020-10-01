using System;

namespace TraderSys.FullStockTickerClientApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly MainWindowViewModel _viewModel;
        
        public MainWindow(MainWindowViewModel viewModel)
        {
            DataContext = _viewModel = viewModel;
            InitializeComponent();
            Closed += OnClosed;
        }

        private async void OnClosed(object sender, EventArgs e)
        {
            await _viewModel.DisposeAsync();
        }
    }
}
