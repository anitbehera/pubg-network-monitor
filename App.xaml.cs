using Microsoft.UI.Xaml;
using PUBGNetworkMonitor;
using PUBGNetworkMonitor.Services;
using PUBGNetworkMonitor.ViewModels;

namespace PUBGNetworkMonitor
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window m_window;
        private OverlayService _overlayService;

        /// <summary>
        /// Shared MainViewModel instance accessible by overlay and main window
        /// </summary>
        public MainViewModel SharedViewModel { get; private set; }

        /// <summary>
        /// Gets the main application window - exposed for file dialogs and other window operations
        /// </summary>
        public Window Window => m_window;

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Initialize shared ViewModel
            SharedViewModel = new MainViewModel();

            m_window = new MainWindow();
            m_window.Activate();

            // Initialize and start the overlay service
            InitializeOverlayService();
        }

        private void InitializeOverlayService()
        {
            _overlayService = new OverlayService();

            // Subscribe to game state changes for debugging
            _overlayService.GameStateChanged += (sender, isRunning) =>
            {
                System.Diagnostics.Debug.WriteLine($"PUBG Game State: {(isRunning ? "Running" : "Not Running")}");
            };

            // Start monitoring for PUBG
            _overlayService.StartMonitoring();
        }

        /// <summary>
        /// Clean up overlay service when main window closes
        /// </summary>
        public void CleanupOverlay()
        {
            _overlayService?.Dispose();
            _overlayService = null;
        }

        /// <summary>
        /// Clean up resources when application exits
        /// </summary>
        ~App()
        {
            CleanupOverlay();
        }
    }
}
