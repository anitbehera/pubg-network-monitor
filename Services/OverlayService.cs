using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace PUBGNetworkMonitor.Services
{
    /// <summary>
    /// Service to manage the PUBG overlay window lifecycle
    /// </summary>
    public class OverlayService : IDisposable
    {
        private readonly Win32WindowService _windowService;
        private readonly DispatcherQueue _dispatcherQueue;
        private Timer _gameDetectionTimer;
        private OverlayWindow _overlayWindow;
        private bool _isOverlayVisible;
        private bool _disposed;

        public event EventHandler<bool> GameStateChanged;

        public OverlayService()
        {
            _windowService = new Win32WindowService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        /// <summary>
        /// Starts monitoring for PUBG and managing overlay visibility
        /// </summary>
        public void StartMonitoring()
        {
            if (_gameDetectionTimer != null)
                return;

            // Check every 2 seconds for PUBG
            _gameDetectionTimer = new Timer(CheckGameState, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// Stops monitoring and hides overlay
        /// </summary>
        public void StopMonitoring()
        {
            _gameDetectionTimer?.Dispose();
            _gameDetectionTimer = null;

            HideOverlay();
        }

        private void CheckGameState(object state)
        {
            if (_disposed)
                return;

            bool isPubgRunning = _windowService.IsPubgRunning();

            // Dispatch to UI thread for window operations
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (isPubgRunning && !_isOverlayVisible)
                {
                    ShowOverlay();
                    GameStateChanged?.Invoke(this, true);
                }
                else if (!isPubgRunning && _isOverlayVisible)
                {
                    HideOverlay();
                    GameStateChanged?.Invoke(this, false);
                }
                else if (isPubgRunning && _isOverlayVisible)
                {
                    // Game is running and overlay is visible, update position
                    UpdateOverlayPosition();
                }
            });
        }

        private void ShowOverlay()
        {
            if (_overlayWindow == null)
            {
                _overlayWindow = new OverlayWindow();
                _overlayWindow.Closed += (s, e) =>
                {
                    _overlayWindow = null;
                    _isOverlayVisible = false;
                };
            }

            _overlayWindow.Activate();
            _isOverlayVisible = true;

            // Position the overlay after a short delay to ensure window is ready
            Task.Delay(100).ContinueWith(_ =>
            {
                _dispatcherQueue.TryEnqueue(UpdateOverlayPosition);
            });
        }

        private void HideOverlay()
        {
            if (_overlayWindow != null)
            {
                try
                {
                    _overlayWindow.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error closing overlay: {ex.Message}");
                }
                _overlayWindow = null;
            }
            _isOverlayVisible = false;
        }

        private void UpdateOverlayPosition()
        {
            if (_overlayWindow?.AppWindow?.Id == null)
                return;

            try
            {
                // Get the overlay window handle
                var overlayHwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(_overlayWindow.AppWindow.Id);

                // Position overlay at bottom-left of PUBG window (32x32 to match button size)
                _windowService.PositionOverlayOnPubg(overlayHwnd, 32, 32);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error positioning overlay: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the main application window for overlay button click handling
        /// </summary>
        public Microsoft.UI.Xaml.Window GetMainWindow()
        {
            return ((App)Microsoft.UI.Xaml.Application.Current).Window;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopMonitoring();
            _overlayWindow?.Close();
        }
    }
}
