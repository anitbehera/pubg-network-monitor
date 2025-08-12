using System;
using System.Net.NetworkInformation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System.Net;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace PUBGNetworkMonitor.Models
{
    /// <summary>
    /// Enhanced TCP connection information with proper process ID resolution
    /// </summary>
    public class TcpConnectionInfo
    {
        public string ProcessName { get; set; }
        public int ProcessId { get; set; }
        public string LocalAddress { get; set; }
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public TcpState State { get; set; }
        public string RemoteHostname { get; set; } = "Resolving...";

        // Computed properties
        public string StateString => State.ToString();
        public bool IsLikelyGameServer => RemotePort >= 7000 && RemotePort <= 8000;

        // UI Brush for connection state
        public Brush StateBrush
        {
            get
            {
                return State switch
                {
                    TcpState.Established => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80)), // Green
                    TcpState.Listen => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 150, 243)), // Blue
                    TcpState.SynSent => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 193, 7)), // Amber
                    TcpState.SynReceived => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 152, 0)), // Orange
                    TcpState.FinWait1 => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 87, 34)), // Deep Orange
                    TcpState.FinWait2 => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)), // Red
                    TcpState.TimeWait => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 121, 85, 72)), // Brown
                    TcpState.Closed => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 96, 125, 139)), // Blue Grey
                    TcpState.CloseWait => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 156, 39, 176)), // Purple
                    TcpState.LastAck => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 233, 30, 99)), // Pink
                    TcpState.Closing => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 103, 58, 183)), // Deep Purple
                    _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 158, 158, 158)) // Grey
                };
            }
        }

        public TcpConnectionInfo() { }

        public TcpConnectionInfo(TcpConnectionInformation tcpInfo)
        {
            LocalAddress = tcpInfo.LocalEndPoint.Address.ToString();
            LocalPort = tcpInfo.LocalEndPoint.Port;
            RemoteAddress = tcpInfo.RemoteEndPoint.Address.ToString();
            RemotePort = tcpInfo.RemoteEndPoint.Port;
            State = tcpInfo.State;

            // Set default values - these will be updated by the service
            ProcessName = "Unknown";
            ProcessId = 0;

            // Start hostname resolution in background
            _ = Task.Run(async () => await ResolveHostnameAsync());
        }

        /// <summary>
        /// Constructor with explicit process information
        /// </summary>
        public TcpConnectionInfo(TcpConnectionInformation tcpInfo, string processName, int processId) : this(tcpInfo)
        {
            ProcessName = processName;
            ProcessId = processId;
        }

        private async Task ResolveHostnameAsync()
        {
            try
            {
                // Skip local addresses
                if (RemoteAddress == "127.0.0.1" || RemoteAddress == "0.0.0.0" ||
                    RemoteAddress.StartsWith("192.168.") || RemoteAddress.StartsWith("10.") ||
                    RemoteAddress.StartsWith("172.16.") || RemoteAddress.StartsWith("172.17.") ||
                    RemoteAddress.StartsWith("172.18.") || RemoteAddress.StartsWith("172.19.") ||
                    RemoteAddress.StartsWith("172.2") || RemoteAddress.StartsWith("172.3") ||
                    RemoteAddress.StartsWith("169.254."))
                {
                    RemoteHostname = "Local/Private";
                    return;
                }

                var hostEntry = await Dns.GetHostEntryAsync(RemoteAddress);
                RemoteHostname = hostEntry.HostName ?? "Unknown";
            }
            catch (Exception)
            {
                RemoteHostname = "Resolution Failed";
            }
        }
    }
}