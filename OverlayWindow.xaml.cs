using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.ComponentModel;

namespace PUBGNetworkMonitor
{
    /// <summary>
    /// Overlay window that appears on top of PUBG
    /// </summary>
    public sealed partial class OverlayWindow : Window
    {
        #region Win32 API for transparency and sizing
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_VISIBLE = 0x10000000;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint LWA_COLORKEY = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }
        #endregion

        private bool _isProcessing = false;
        private bool _initialSizeSet = false;

        public OverlayWindow()
        {
            this.InitializeComponent();
            InitializeOverlayWindow();
        }

        private void InitializeOverlayWindow()
        {
            if (this.AppWindow != null)
            {
                // Configure presenter for overlay behavior first
                if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                    presenter.SetBorderAndTitleBar(false, false);
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                }

                this.Title = "PUBG Utility Overlay";

                // Initial attempt to size (may run too early)
                try
                {
                    this.AppWindow.Resize(new Windows.Graphics.SizeInt32(32, 32));
                }
                catch { /* ignore */ }

                // Hook Activated to finalize sizing & transparency
                this.Activated += OverlayWindow_Activated;
            }

            this.SystemBackdrop = null;
        }

        private void OverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Only run once
            this.Activated -= OverlayWindow_Activated;

            // First enforce correct size
            ApplyFinalSizing();

            // Then apply transparency after brief delay
            var timer = new System.Threading.Timer((_) =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyTransparency();
                    // Enforce size again post-transparency
                    ApplyFinalSizing();
                });
            }, null, 100, System.Threading.Timeout.Infinite);
        }

        private void ApplyFinalSizing()
        {
            if (_initialSizeSet) return;
            try
            {
                var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
                if (hwnd != IntPtr.Zero)
                {
                    // Win32 sizing
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 32, 32, SWP_NOMOVE | SWP_NOZORDER);
                    // WinUI sizing
                    this.AppWindow.Resize(new Windows.Graphics.SizeInt32(32, 32));
                    _initialSizeSet = true;
                }
            }
            catch { /* ignore */ }
        }

        private void ApplyTransparency()
        {
            try
            {
                var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(this.AppWindow.Id);
                if (hwnd != IntPtr.Zero)
                {
                    // Popup style
                    SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
                    // Layered, toolwindow, no activate
                    SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER);

                    var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                    DwmExtendFrameIntoClientArea(hwnd, ref margins);
                    SetLayeredWindowAttributes(hwnd, 0x010101, 255, LWA_COLORKEY);
                }
            }
            catch { /* ignore */ }
        }

        private async void OverlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;

            ShowSpinner();
            var app = (App)Application.Current;
            var viewModel = app.SharedViewModel;

            if (viewModel != null && viewModel.CloseLobbyConnectionsCommand.CanExecute(null))
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                viewModel.CloseLobbyConnectionsCommand.Execute(null);

                var mainWindow = app.Window;
                mainWindow?.Activate();
                if (mainWindow?.AppWindow?.Presenter is OverlappedPresenter mainPres && mainPres.State == OverlappedPresenterState.Minimized)
                    mainPres.Restore();
            }
            else
            {
                HideSpinner();
                _isProcessing = false;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var viewModel = ((App)Application.Current).SharedViewModel;
            if (e.PropertyName == nameof(viewModel.IsLoading) && !viewModel.IsLoading)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    HideSpinner();
                    _isProcessing = false;
                    viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                });
            }
        }

        private void ShowSpinner()
        {
            OverlayButton.Visibility = Visibility.Collapsed;
            SpinnerOverlay.Visibility = Visibility.Visible;
            LoadingSpinner.IsActive = true;
        }

        private void HideSpinner()
        {
            LoadingSpinner.IsActive = false;
            SpinnerOverlay.Visibility = Visibility.Collapsed;
            OverlayButton.Visibility = Visibility.Visible;
        }
    }
}
