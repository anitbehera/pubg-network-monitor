using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using PUBGNetworkMonitor.Models;
using System.Runtime.InteropServices;
using System.Net;

namespace PUBGNetworkMonitor.Services
{
    /// <summary>
    /// Service for retrieving TCP connection information with proper process matching
    /// </summary>
    public class TcpConnectionService
    {
        // Windows API structures and functions for getting process information from TCP connections
        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
            public uint dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 1)]
            public MIB_TCPROW_OWNER_PID[] table;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort,
            int ipVersion, TCP_TABLE_CLASS tblClass, int reserved);

        private enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }

        /// <summary>
        /// Gets TCP connections for TslGame processes with proper process ID matching
        /// </summary>
        public async Task<IEnumerable<TcpConnectionInfo>> GetTslGameConnectionsAsync()
        {
            try
            {
                // Get TslGame processes first
                var tslGameProcesses = Process.GetProcessesByName("TslGame").ToList();
                if (!tslGameProcesses.Any())
                {
                    Debug.WriteLine("No TslGame processes found");
                    return new List<TcpConnectionInfo>();
                }

                var tslGamePids = tslGameProcesses.Select(p => (uint)p.Id).ToHashSet();
                var connections = new List<TcpConnectionInfo>();

                // Get TCP connections with process IDs using Windows API
                var tcpConnections = GetTcpConnectionsWithPid();

                foreach (var tcpConn in tcpConnections)
                {
                    // Only include connections from TslGame processes
                    if (tslGamePids.Contains(tcpConn.dwOwningPid))
                    {
                        var tslProcess = tslGameProcesses.FirstOrDefault(p => p.Id == tcpConn.dwOwningPid);
                        if (tslProcess != null)
                        {
                            // Convert Windows API structure to our model
                            var connectionInfo = CreateConnectionInfo(tcpConn, tslProcess.ProcessName, (int)tcpConn.dwOwningPid);
                            connections.Add(connectionInfo);
                        }
                    }
                }

                // Clean up process resources
                foreach (var process in tslGameProcesses)
                {
                    process?.Dispose();
                }

                return connections;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting TslGame connections: {ex.Message}");
                // Fallback to the old method if Windows API fails
                return await GetTslGameConnectionsFallbackAsync();
            }
        }

        /// <summary>
        /// Fallback method using .NET's built-in TCP connection enumeration
        /// </summary>
        private async Task<IEnumerable<TcpConnectionInfo>> GetTslGameConnectionsFallbackAsync()
        {
            try
            {
                var tslGameProcesses = Process.GetProcessesByName("TslGame").ToList();
                if (!tslGameProcesses.Any())
                {
                    return new List<TcpConnectionInfo>();
                }

                // Get all TCP connections from the system
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnections = ipProperties.GetActiveTcpConnections();

                var connections = new List<TcpConnectionInfo>();

                // Since we can't directly match connections to processes with the built-in API,
                // we'll create connections and assign them to TslGame processes
                var mainTslProcess = tslGameProcesses.First();

                foreach (var tcpConn in tcpConnections)
                {
                    // Create connection info with the main TslGame process info
                    var connectionInfo = new TcpConnectionInfo(tcpConn, "TslGame", mainTslProcess.Id);
                    connections.Add(connectionInfo);
                }

                // Clean up process resources
                foreach (var process in tslGameProcesses)
                {
                    process?.Dispose();
                }

                return connections;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in fallback method: {ex.Message}");
                return new List<TcpConnectionInfo>();
            }
        }

        /// <summary>
        /// Gets TCP connections with process IDs using Windows API
        /// </summary>
        private List<MIB_TCPROW_OWNER_PID> GetTcpConnectionsWithPid()
        {
            var tcpConnections = new List<MIB_TCPROW_OWNER_PID>();
            int bufferSize = 0;

            // First call to get the buffer size
            uint result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

            if (bufferSize > 0)
            {
                IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    result = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

                    if (result == 0)
                    {
                        // Get the number of entries
                        uint numEntries = (uint)Marshal.ReadInt32(tcpTablePtr);

                        // Read each TCP connection entry
                        IntPtr currentPtr = IntPtr.Add(tcpTablePtr, 4); // Skip the count
                        int structSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

                        for (int i = 0; i < numEntries; i++)
                        {
                            var tcpRow = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(currentPtr);
                            tcpConnections.Add(tcpRow);
                            currentPtr = IntPtr.Add(currentPtr, structSize);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(tcpTablePtr);
                }
            }

            return tcpConnections;
        }

        /// <summary>
        /// Creates a TcpConnectionInfo from Windows API structure
        /// </summary>
        private TcpConnectionInfo CreateConnectionInfo(MIB_TCPROW_OWNER_PID tcpRow, string processName, int processId)
        {
            var connectionInfo = new TcpConnectionInfo
            {
                ProcessName = processName,
                ProcessId = processId,
                LocalAddress = new IPAddress(tcpRow.dwLocalAddr).ToString(),
                LocalPort = ntohs((ushort)tcpRow.dwLocalPort),
                RemoteAddress = new IPAddress(tcpRow.dwRemoteAddr).ToString(),
                RemotePort = ntohs((ushort)tcpRow.dwRemotePort),
                State = (TcpState)tcpRow.dwState,
                RemoteHostname = "Resolving..."
            };

            // Start hostname resolution in background
            _ = Task.Run(async () => await ResolveHostnameForConnection(connectionInfo));

            return connectionInfo;
        }

        /// <summary>
        /// Resolves hostname for a connection
        /// </summary>
        private async Task ResolveHostnameForConnection(TcpConnectionInfo connection)
        {
            try
            {
                // Skip local addresses
                if (connection.RemoteAddress == "127.0.0.1" || connection.RemoteAddress == "0.0.0.0" ||
                    connection.RemoteAddress.StartsWith("192.168.") || connection.RemoteAddress.StartsWith("10.") ||
                    connection.RemoteAddress.StartsWith("172.16.") || connection.RemoteAddress.StartsWith("172.17.") ||
                    connection.RemoteAddress.StartsWith("172.18.") || connection.RemoteAddress.StartsWith("172.19.") ||
                    connection.RemoteAddress.StartsWith("172.2") || connection.RemoteAddress.StartsWith("172.3") ||
                    connection.RemoteAddress.StartsWith("169.254."))
                {
                    connection.RemoteHostname = "Local/Private";
                    return;
                }

                var hostEntry = await Dns.GetHostEntryAsync(connection.RemoteAddress);
                connection.RemoteHostname = hostEntry.HostName ?? "Unknown";
            }
            catch (Exception)
            {
                connection.RemoteHostname = "Resolution Failed";
            }
        }

        /// <summary>
        /// Converts network byte order to host byte order
        /// </summary>
        private static ushort ntohs(ushort netshort)
        {
            return (ushort)(((netshort & 0xFF) << 8) | ((netshort >> 8) & 0xFF));
        }

        /// <summary>
        /// Gets all TCP connections (not filtered by process)
        /// </summary>
        public async Task<IEnumerable<TcpConnectionInfo>> GetAllTcpConnectionsAsync()
        {
            try
            {
                var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnections = ipProperties.GetActiveTcpConnections();

                var connections = tcpConnections.Select(tcpConn => new TcpConnectionInfo(tcpConn));

                return connections;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting all TCP connections: {ex.Message}");
                return new List<TcpConnectionInfo>();
            }
        }

        /// <summary>
        /// Checks if TslGame process is currently running
        /// </summary>
        public bool IsTslGameRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("TslGame");
                bool isRunning = processes.Length > 0;

                foreach (var process in processes)
                {
                    process?.Dispose();
                }

                return isRunning;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking TslGame process: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets connection statistics
        /// </summary>
        public ConnectionStatistics GetConnectionStatistics(IEnumerable<TcpConnectionInfo> connections)
        {
            var connectionList = connections.ToList();

            return new ConnectionStatistics
            {
                TotalConnections = connectionList.Count,
                EstablishedConnections = connectionList.Count(c => c.State == TcpState.Established),
                ListeningConnections = connectionList.Count(c => c.State == TcpState.Listen),
                GameServerConnections = connectionList.Count(c => c.IsLikelyGameServer),
                UniqueRemoteAddresses = connectionList.Select(c => c.RemoteAddress).Distinct().Count(),
                UniqueRemoteHostnames = connectionList
                    .Where(c => !string.IsNullOrEmpty(c.RemoteHostname) &&
                               c.RemoteHostname != "Resolving..." &&
                               c.RemoteHostname != "Resolution Failed")
                    .Select(c => c.RemoteHostname)
                    .Distinct()
                    .Count()
            };
        }
    }

    /// <summary>
    /// Statistics about TCP connections
    /// </summary>
    public class ConnectionStatistics
    {
        public int TotalConnections { get; set; }
        public int EstablishedConnections { get; set; }
        public int ListeningConnections { get; set; }
        public int GameServerConnections { get; set; }
        public int UniqueRemoteAddresses { get; set; }
        public int UniqueRemoteHostnames { get; set; }
    }
}