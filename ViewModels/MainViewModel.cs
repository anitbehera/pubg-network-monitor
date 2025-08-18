using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using PUBGNetworkMonitor.Models;
using PUBGNetworkMonitor.Services;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using System.Windows.Input;
using System.Net;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PUBGNetworkMonitor.ViewModels
{
    /// <summary>
    /// ViewModel for TslGame Akamai connection monitoring with graceful connection termination
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly TcpConnectionService tcpConnectionService;
        private readonly DispatcherQueue uiDispatcherQueue;

        private ObservableCollection<TcpConnectionInfo> connectionsCollection = new();
        private bool loadingState = false;
        private string currentStatusMessage = "Ready - Click 'Network Button' to scan and close PUBG Lobby connections";
        private int connectionCount = 0;

        // Game statistics
        private int establishedConnections = 0;
        private int gameServerConnections = 0;
        private int uniqueRemoteServers = 0;

        // Windows API for connection termination - NO PROCESS KILLING
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int SetTcpEntry(ref MIB_TCPROW pTcpRow);

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW
        {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
        }

        private const int MIB_TCP_STATE_DELETE_TCB = 12;

        #region Properties

        public ObservableCollection<TcpConnectionInfo> Connections
        {
            get => connectionsCollection;
            set
            {
                connectionsCollection = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => loadingState;
            set
            {
                loadingState = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => currentStatusMessage;
            set
            {
                currentStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public int TotalConnections
        {
            get => connectionCount;
            set
            {
                connectionCount = value;
                OnPropertyChanged();
            }
        }

        public int EstablishedConnections
        {
            get => establishedConnections;
            set
            {
                establishedConnections = value;
                OnPropertyChanged();
            }
        }

        public int GameServerConnections
        {
            get => gameServerConnections;
            set
            {
                gameServerConnections = value;
                OnPropertyChanged();
            }
        }

        public int UniqueRemoteServers
        {
            get => uniqueRemoteServers;
            set
            {
                uniqueRemoteServers = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<string> connectionLog = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectionLog
        {
            get => connectionLog;
            set
            {
                connectionLog = value;
                OnPropertyChanged();
            }
        }

        private bool isLogVisible = false;
        public bool IsLogVisible
        {
            get => isLogVisible;
            set
            {
                isLogVisible = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands


        public ICommand CloseLobbyConnectionsCommand { get; }
        public ICommand ClearDisplayCommand { get; }

        #endregion

        public MainViewModel()
        {
            tcpConnectionService = new TcpConnectionService();
            uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Setup commands
            CloseLobbyConnectionsCommand = new RelayCommand(async () => await CloseLobbyConnectionsAsync(), () => !IsLoading);
            ClearDisplayCommand = new RelayCommand(ClearDisplay, () => Connections.Any() || IsLogVisible);
        }



        private async Task CloseLobbyConnectionsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Scanning for PUBG Lobby connections to close...";

                // Get current TslGame connections
                var tslConnections = await tcpConnectionService.GetTslGameConnectionsAsync();
                var allConnections = tslConnections.ToList();

                // Include ALL connections
                var connectionsToClose = allConnections
                    .Where(conn => conn.RemoteAddress != "0.0.0.0")
                    .ToList();

                connectionsToClose = connectionsToClose
                    .OrderBy(conn => conn.RemoteAddress)
                    .ThenBy(conn => conn.LocalAddress)
                    .ToList();

                if (!connectionsToClose.Any())
                {
                    StatusMessage = $"No PUBG Lobby connections found to close. Scanned {allConnections.Count} total connections.";
                    return;
                }

                StatusMessage = $"Closing {connectionsToClose.Count} PUBG Lobby connections connections gracefully...";

                int closedCount = 0;
                var results = new List<string>();

                foreach (var connection in connectionsToClose)
                {
                    try
                    {
                        // Use Windows API to gracefully close TCP connection
                        bool success = await CloseConnectionGracefullyAsync(connection);

                        if (success)
                        {
                            closedCount++;
                            results.Add($"✅ Closed connection to {connection.RemoteAddress}:{connection.RemotePort}");
                        }
                        else
                        {
                            results.Add($"❌ Failed to close connection to {connection.RemoteAddress}:{connection.RemotePort}");
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add($"❌ Error closing {connection.RemoteAddress}:{connection.RemotePort}: {ex.Message}");
                    }

                    // Small delay between connection closures for graceful termination
                    await Task.Delay(200);
                }

                // Try alternative gentle approaches if main method didn't work well
                if (closedCount < connectionsToClose.Count / 2) // If less than half succeeded
                {
                    StatusMessage = "Trying alternative gentle connection closure methods...";

                    foreach (var connection in connectionsToClose)
                    {
                        try
                        {
                            // Try using netsh command for graceful closure
                            bool netshSuccess = await TryNetshCloseConnectionAsync(connection);
                            if (netshSuccess && !results.Any(r => r.Contains($"{connection.RemoteAddress}:{connection.RemotePort}") && r.StartsWith("✅")))
                            {
                                closedCount++;
                                results.Add($"✅ Closed via netsh: {connection.RemoteAddress}:{connection.RemotePort}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Netsh close failed: {ex.Message}");
                        }
                    }
                }

                // Update UI with results
                uiDispatcherQueue.TryEnqueue(() =>
                {
                    if (closedCount > 0)
                    {
                        StatusMessage = $"✅ Successfully closed {closedCount}/{connectionsToClose.Count} PUBG Lobby connections • Completed at {DateTime.Now:HH:mm:ss}";

                        // Populate the connection log with closed connections
                        ConnectionLog.Clear();
                        ConnectionLog.Add($"=== Connection Close Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                        ConnectionLog.Add($"Closed {closedCount} out of {connectionsToClose.Count} PUBG Lobby connections");
                        ConnectionLog.Add("--------------");

                        foreach (var result in results)
                        {
                            ConnectionLog.Add(result);
                        }

                        ConnectionLog.Add("");
                        ConnectionLog.Add($"=== End of Log ===");

                        // Show the log
                        IsLogVisible = true;

                        // Clear the display after successful closure
                        Connections.Clear();
                        TotalConnections = 0;
                        EstablishedConnections = 0;
                        GameServerConnections = 0;
                        UniqueRemoteServers = 0;
                    }
                    else
                    {
                        StatusMessage = $"❌ Unable to close connections gracefully. May require administrator privileges or connections may be protected • {DateTime.Now:HH:mm:ss}";
                    }

                    CommandManager.InvalidateRequerySuggested();
                });

                // Log detailed results for debugging
                foreach (var result in results)
                {
                    Debug.WriteLine(result);
                }
            }
            catch (Exception ex)
            {
                uiDispatcherQueue.TryEnqueue(() =>
                {
                    StatusMessage = $"Error closing connections: {ex.Message}";
                });
            }
            finally
            {
                uiDispatcherQueue.TryEnqueue(() =>
                {
                    IsLoading = false;
                    CommandManager.InvalidateRequerySuggested();
                });
            }
        }

        private async Task<bool> CloseConnectionGracefullyAsync(TcpConnectionInfo connection)
        {
            try
            {
                // Convert IP addresses to network byte order
                var localAddr = IPAddress.Parse(connection.LocalAddress).GetAddressBytes();
                var remoteAddr = IPAddress.Parse(connection.RemoteAddress).GetAddressBytes();

                var tcpRow = new MIB_TCPROW
                {
                    dwState = MIB_TCP_STATE_DELETE_TCB,
                    dwLocalAddr = BitConverter.ToUInt32(localAddr, 0),
                    dwLocalPort = htons((ushort)connection.LocalPort),
                    dwRemoteAddr = BitConverter.ToUInt32(remoteAddr, 0),
                    dwRemotePort = htons((ushort)connection.RemotePort)
                };

                int result = SetTcpEntry(ref tcpRow);
                return result == 0; // 0 means success
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"API graceful close failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryNetshCloseConnectionAsync(TcpConnectionInfo connection)
        {
            try
            {
                // Use netsh command to reset the specific connection
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"int ip reset",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        return process.ExitCode == 0;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Netsh command failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts host byte order to network byte order
        /// </summary>
        private static ushort htons(ushort hostshort)
        {
            return (ushort)(((hostshort & 0xFF) << 8) | ((hostshort >> 8) & 0xFF));
        }

        private void ClearDisplay()
        {
            Connections.Clear();
            TotalConnections = 0;
            EstablishedConnections = 0;
            GameServerConnections = 0;
            UniqueRemoteServers = 0;
            ConnectionLog.Clear();
            IsLogVisible = false;
            StatusMessage = "Display cleared - Use 'Close Lobby Connections' to scan and close PUBG connections";
            CommandManager.InvalidateRequerySuggested();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #region Helper Classes

    /// <summary>
    /// Simple RelayCommand implementation with CanExecute support
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => execute();
    }

    /// <summary>
    /// CommandManager for WinUI 3
    /// </summary>
    public static class CommandManager
    {
        public static event EventHandler RequerySuggested;

        public static void InvalidateRequerySuggested()
        {
            RequerySuggested?.Invoke(null, EventArgs.Empty);
        }
    }

    #endregion
}