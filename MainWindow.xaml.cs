using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace PUBGNetworkMonitor
{
    /// <summary>
    /// Main application window for PUBG Utility
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "PUBG Network Monitor";

            // Set window icon
            this.AppWindow.SetIcon("Assets/logo.ico");

            // Desired window size
            var desiredSize = new SizeInt32(700, 400);

            if (this.AppWindow != null)
            {
                // Resize to fixed size
                this.AppWindow.Resize(desiredSize);

                // Center on primary display
                var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;  // RectInt32

                // Calculate centered position
                int centerX = workArea.X + (workArea.Width - desiredSize.Width) / 2;
                int centerY = workArea.Y + (workArea.Height - desiredSize.Height) / 2;

                this.AppWindow.Move(new PointInt32(centerX, centerY));
            }

            // Configure window presenter for fixed size
            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }

            // Navigate to dashboard initially
            ContentFrame.Navigate(typeof(Views.DashboardPage));

            // Handle window closing to cleanup overlay
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            // Notify the app to cleanup overlay service when main window closes
            ((App)Application.Current).CleanupOverlay();
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                switch (selectedItem.Tag?.ToString())
                {
                    case "dashboard":
                        ContentFrame.Navigate(typeof(Views.DashboardPage));
                        break;
                    case "logs":
                        ContentFrame.Navigate(typeof(Views.LogsViewerPage));
                        break;
                    case "about":
                        ContentFrame.Navigate(typeof(Views.AboutPage));
                        break;
                }
            }
        }
    }
}

