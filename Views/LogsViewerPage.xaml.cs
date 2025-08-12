using Microsoft.UI.Xaml.Controls;
using PUBGNetworkMonitor.ViewModels;

namespace PUBGNetworkMonitor.Views;

public sealed partial class LogsViewerPage : Page
{
    // This property will hold the shared view model instance.
    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public LogsViewerPage()
    {
        this.InitializeComponent();
        // Set DataContext to the shared ViewModel instance
        this.DataContext = ((App)Microsoft.UI.Xaml.Application.Current).SharedViewModel;
    }
}