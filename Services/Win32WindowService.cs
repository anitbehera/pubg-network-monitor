using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PUBGNetworkMonitor.Services
{
    /// <summary>
    /// Service for Win32 window manipulation and process detection
    /// </summary>
    public class Win32WindowService
    {
        #region Win32 API Declarations

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr hwnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // SetWindowPos flags
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOREDRAW = 0x0008;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_HIDEWINDOW = 0x0080;
        private const uint SWP_NOCOPYBITS = 0x0100;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOSENDCHANGING = 0x0400;

        // Special window handles
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);

        #endregion

        /// <summary>
        /// Checks if PUBG is currently running and returns the main window handle
        /// </summary>
        /// <returns>PUBG window handle or IntPtr.Zero if not found</returns>
        public IntPtr GetPubgWindowHandle()
        {
            // Common PUBG process names
            string[] pubgProcessNames = { "TslGame", "PUBG", "PUBG_BE" };

            foreach (string processName in pubgProcessNames)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    foreach (Process process in processes)
                    {
                        if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
                        {
                            return process.MainWindowHandle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking process {processName}: {ex.Message}");
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// For testing: finds any visible window to simulate PUBG for demo purposes
        /// Remove this method when testing with actual PUBG
        /// </summary>
        public IntPtr GetAnyVisibleWindowForTesting()
        {
            try
            {
                // Find any visible window for testing (like notepad, calculator, etc.)
                Process[] processes = Process.GetProcesses();
                foreach (Process process in processes)
                {
                    if (process.MainWindowHandle != IntPtr.Zero &&
                        IsWindowVisible(process.MainWindowHandle) &&
                        !string.IsNullOrEmpty(process.MainWindowTitle) &&
                        process.ProcessName != "PUBGNetworkMonitor") // Don't target own app
                    {
                        Debug.WriteLine($"Found test window: {process.ProcessName} - {process.MainWindowTitle}");
                        return process.MainWindowHandle;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding test window: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the window rectangle for the specified window handle
        /// </summary>
        /// <param name="hwnd">Window handle</param>
        /// <returns>Window rectangle or null if failed</returns>
        public RECT? GetWindowRect(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                return null;

            if (GetWindowRect(hwnd, out RECT rect))
                return rect;

            return null;
        }

        /// <summary>
        /// Gets the client area rectangle in screen coordinates
        /// </summary>
        /// <param name="hwnd">Window handle</param>
        /// <returns>Client rectangle in screen coordinates or null if failed</returns>
        public RECT? GetClientAreaRect(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                return null;

            if (GetClientRect(hwnd, out RECT clientRect))
            {
                // Convert client area to screen coordinates
                var topLeft = new POINT { X = 0, Y = 0 };
                var bottomRight = new POINT { X = clientRect.Right, Y = clientRect.Bottom };

                if (ClientToScreen(hwnd, ref topLeft) && ClientToScreen(hwnd, ref bottomRight))
                {
                    return new RECT
                    {
                        Left = topLeft.X,
                        Top = topLeft.Y,
                        Right = bottomRight.X,
                        Bottom = bottomRight.Y
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if PUBG is currently running
        /// </summary>
        /// <returns>True if PUBG is running and visible</returns>
        public bool IsPubgRunning()
        {
            return GetPubgWindowHandle() != IntPtr.Zero;
        }

        /// <summary>
        /// Sets a window to be always on top
        /// </summary>
        /// <param name="hwnd">Window handle to set topmost</param>
        /// <returns>True if successful</returns>
        public bool SetWindowTopmost(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            return SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Positions a window at the bottom-left of the PUBG window
        /// </summary>
        /// <param name="overlayHwnd">Handle of the overlay window to position</param>
        /// <param name="overlayWidth">Width of the overlay window</param>
        /// <param name="overlayHeight">Height of the overlay window</param>
        /// <returns>True if positioned successfully</returns>
        public bool PositionOverlayOnPubg(IntPtr overlayHwnd, int overlayWidth, int overlayHeight)
        {
            IntPtr pubgHwnd = GetPubgWindowHandle();
            if (pubgHwnd == IntPtr.Zero || overlayHwnd == IntPtr.Zero)
                return false;

            // Try to get client area first (game content area without borders)
            RECT? pubgRect = GetClientAreaRect(pubgHwnd);
            if (!pubgRect.HasValue)
            {
                // Fallback to window rectangle if client area fails
                pubgRect = GetWindowRect(pubgHwnd);
                if (!pubgRect.HasValue)
                    return false;
            }

            // Calculate position at the very left edge of game window with no gap
            int x = pubgRect.Value.Left; // Perfectly flush with left edge of PUBG game window
            int y = pubgRect.Value.Bottom - overlayHeight - 80; // Bring down to align perfectly with EXIT TO LOBBY button

            Debug.WriteLine($"Positioning overlay at ({x}, {y}) for PUBG rect: Left={pubgRect.Value.Left}, Bottom={pubgRect.Value.Bottom}");

            // Set the overlay window position and make it topmost
            return SetWindowPos(overlayHwnd, HWND_TOPMOST, x, y, overlayWidth, overlayHeight,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Gets the window title for debugging purposes
        /// </summary>
        /// <param name="hwnd">Window handle</param>
        /// <returns>Window title or empty string</returns>
        public string GetWindowTitle(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return string.Empty;

            var title = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, title, title.Capacity);
            return title.ToString();
        }
    }
}
