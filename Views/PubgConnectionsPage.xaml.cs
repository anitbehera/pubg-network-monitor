using Microsoft.UI.Xaml.Controls;
using PUBGNetworkMonitor.ViewModels;

namespace PUBGNetworkMonitor.Views
{
    /// <summary>
    /// Main page for monitoring PUBG network connections
    /// </summary>
    public sealed partial class PubgConnectionsPage : Page
    {
        public MainViewModel ViewModel => (MainViewModel)DataContext;

        public PubgConnectionsPage()
        {
            this.InitializeComponent();

            // Set DataContext to the shared ViewModel instance
            this.DataContext = ((App)Microsoft.UI.Xaml.Application.Current).SharedViewModel;
        }
    }
}