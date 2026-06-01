using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LaptopForensics.Core.Helpers;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;

namespace LaptopForensics.Core.Scanners;

public class NetworkScanner : IScanModule
{
    private readonly IPowerShellService _ps;
    private readonly ILogger<NetworkScanner> _logger;

    public string ModuleName => "NetworkScanner";
    public string ModuleIcon => "🌐";
    public int EstimatedSeconds => 12;
    public ScanMode ApplicableModes => ScanMode.Full;

    public NetworkScanner(IPowerShellService ps, ILogger<NetworkScanner> logger)
    {
        _ps = ps;
        _logger = logger;
    }

    public async Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new NetworkInfo();
        var findings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 1. Adapters
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var iface in interfaces)
                {
                    var adapter = new NetworkAdapter
                    {
                        Name = iface.Name,
                        Type = iface.NetworkInterfaceType.ToString(),
                        MacAddress = string.Join(":", iface.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))),
                        IsConnected = iface.OperationalStatus == OperationalStatus.Up,
                        SpeedMbps = iface.Speed / 1_000_000
                    };
                    
                    var props = iface.GetIPProperties();
                    adapter.DnsServers = props.DnsAddresses.Select(d => d.ToString()).ToList();
                    
                    var ipv4Props = props.GetIPv4Properties();
                    if (ipv4Props != null)
                    {
                        adapter.DhcpEnabled = ipv4Props.IsDhcpEnabled;
                    }
                    
                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            adapter.IpAddresses.Add(addr.Address.ToString());
                            adapter.SubnetMask = addr.IPv4Mask?.ToString() ?? "";
                        }
                    }
                    
                    foreach (var gw in props.GatewayAddresses)
                    {
                        if (gw.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            adapter.DefaultGateway = gw.Address.ToString();
                            break;
                        }
                    }

                    if (iface.OperationalStatus == OperationalStatus.Up)
                    {
                        var stats = iface.GetIPStatistics();
                        adapter.BytesSent = stats.BytesSent;
                        adapter.BytesReceived = stats.BytesReceived;
                    }

                    if (adapter.Type.Contains("Wireless", StringComparison.OrdinalIgnoreCase))
                    {
                        adapter.Type = "WiFi";
                    }

                    resultData.Adapters.Add(adapter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get network adapters.");
            }

            // 2. TCP Connections & Mapping
            try
            {
                var tcpRows = GetAllTcpConnections();
                var activeConnections = new List<TcpConnectionInfo>();
                
                foreach (var row in tcpRows)
                {
                    var info = new TcpConnectionInfo
                    {
                        LocalAddress = row.LocalAddress.ToString(),
                        LocalPort = row.LocalPort,
                        RemoteAddress = row.RemoteAddress.ToString(),
                        RemotePort = row.RemotePort,
                        State = row.State.ToString(),
                        Pid = row.ProcessId
                    };
                    
                    try
                    {
                        if (info.Pid > 0)
                        {
                            var proc = Process.GetProcessById(info.Pid.Value);
                            info.ProcessName = proc.ProcessName;
                        }
                    }
                    catch { /* process may have exited or access denied */ }
                    
                    activeConnections.Add(info);
                }
                
                // DNS Resolution in parallel
                var uniqueIps = activeConnections
                    .Where(c => c.State == "Established" && c.RemoteAddress != "0.0.0.0" && c.RemoteAddress != "127.0.0.1" && c.RemoteAddress != "::1")
                    .Select(c => c.RemoteAddress)
                    .Distinct()
                    .ToList();
                
                var dnsCache = new Dictionary<string, string>();
                var throttler = new SemaphoreSlim(10);
                
                var lookupTasks = uniqueIps.Select(async ip =>
                {
                    await throttler.WaitAsync(ct);
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(1000);
                        
                        var hostEntry = await Dns.GetHostEntryAsync(ip, cts.Token);
                        if (!string.IsNullOrEmpty(hostEntry.HostName))
                        {
                            lock (dnsCache) { dnsCache[ip] = hostEntry.HostName; }
                        }
                    }
                    catch { /* Ignore dns timeout/failure */ }
                    finally
                    {
                        throttler.Release();
                    }
                });
                
                await Task.WhenAll(lookupTasks);
                
                foreach (var conn in activeConnections)
                {
                    if (dnsCache.TryGetValue(conn.RemoteAddress, out var hostname))
                    {
                        conn.RemoteHostname = hostname;
                    }
                }
                
                resultData.Connections = activeConnections;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get TCP connections.");
            }

            // 5. UDP Listeners
            try
            {
                var ipGlobalProps = IPGlobalProperties.GetIPGlobalProperties();
                var udpEndpoints = ipGlobalProps.GetActiveUdpListeners();
                
                foreach (var ep in udpEndpoints)
                {
                    var udpInfo = new UdpPortInfo
                    {
                        LocalAddress = ep.Address.ToString(),
                        LocalPort = ep.Port
                    };
                    resultData.ListeningPorts.Add(udpInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get UDP listeners.");
            }

            // 6. Saved WiFi Networks
            try
            {
                var output = await _ps.RunAsync("netsh wlan show profiles", 10);
                if (!string.IsNullOrEmpty(output))
                {
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("All User Profile"))
                        {
                            var parts = line.Split(':');
                            if (parts.Length > 1)
                            {
                                var profileName = parts[1].Trim();
                                var profileOutput = await _ps.RunAsync($"netsh wlan show profile name=\"{profileName}\" key=clear", 10);
                                var security = "Unknown";
                                if (profileOutput.Contains("Authentication"))
                                {
                                    var authLine = profileOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault(l => l.Contains("Authentication"));
                                    if (authLine != null)
                                    {
                                        security = authLine.Split(':').Last().Trim();
                                    }
                                }
                                resultData.SavedNetworks.Add(new SavedWifiNetwork { Name = profileName, Security = security });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get WiFi profiles.");
            }

            // 7. Ping Test
            try
            {
                using var ping = new Ping();
                int successCount = 0;
                long sum = 0, min = long.MaxValue, max = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    var reply = await ping.SendPingAsync("8.8.8.8", 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        successCount++;
                        sum += reply.RoundtripTime;
                        if (reply.RoundtripTime < min) min = reply.RoundtripTime;
                        if (reply.RoundtripTime > max) max = reply.RoundtripTime;
                    }
                }
                
                if (successCount > 0)
                {
                    resultData.PingTest.AverageMs = (double)sum / successCount;
                    resultData.PingTest.MinMs = min;
                    resultData.PingTest.MaxMs = max;
                }
                resultData.PingTest.PacketLoss = (5 - successCount) * 100 / 5;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to run ping test.");
                resultData.PingTest.PacketLoss = 100;
            }

            // 8. Firewall Rule Count
            try
            {
                var ruleCountOutput = await _ps.RunAsync("(Get-NetFirewallRule).Count", 10);
                if (int.TryParse(ruleCountOutput.Trim(), out var count))
                {
                    resultData.FirewallRuleCount = count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get firewall rule count.");
            }

            // 9. Scoring and Findings
            var hasSuspicious = false;
            foreach (var conn in resultData.Connections)
            {
                if (conn.RemoteAddress != "0.0.0.0" && conn.RemoteAddress != "127.0.0.1" && conn.RemoteAddress != "::1" && conn.State == "Established")
                {
                    // Simulated suspicious check (could be expanded)
                }
            }

            if (hasSuspicious)
            {
                score -= 20;
                findings.Add(new Finding { Level = Severity.Critical, Title = "Suspicious outbound connection", Description = "Connection to known malicious IP range", Recommendation = "Investigate the process making this connection immediately." });
            }

            bool firewallDisabled = false;
            try
            {
                var fwOut = await _ps.RunAsync("Get-NetFirewallProfile | Select Name,Enabled", 10);
                if (fwOut.Contains("False"))
                {
                    firewallDisabled = true;
                }
            }
            catch {}

            if (firewallDisabled)
            {
                score -= 15;
            }

            var smbOpen = resultData.Connections.Any(c => c.LocalPort == 445 && c.LocalAddress == "0.0.0.0" && c.State == "Listen") || resultData.ListeningPorts.Any(c => c.LocalPort == 445 && c.LocalAddress == "0.0.0.0");
            if (smbOpen)
            {
                score -= 15;
                findings.Add(new Finding { Level = Severity.Warning, Title = "File sharing exposed", Description = "SMB open on all interfaces", Recommendation = "Disable SMB or restrict to local network." });
            }

            var rdpOpen = resultData.Connections.Any(c => c.LocalPort == 3389 && c.LocalAddress == "0.0.0.0" && c.State == "Listen") || resultData.ListeningPorts.Any(c => c.LocalPort == 3389 && c.LocalAddress == "0.0.0.0");
            if (rdpOpen)
            {
                score -= 10;
                findings.Add(new Finding { Level = Severity.Warning, Title = "Remote Desktop exposed", Description = "RDP open on all interfaces", Recommendation = "Disable RDP or restrict to VPN/local network." });
            }

            var telnetOpen = resultData.Connections.Any(c => c.LocalPort == 23 && c.State == "Listen") || resultData.ListeningPorts.Any(c => c.LocalPort == 23);
            if (telnetOpen)
            {
                score -= 10;
                findings.Add(new Finding { Level = Severity.Critical, Title = "Insecure protocol enabled", Description = "Telnet port 23 open externally", Recommendation = "Disable Telnet and use SSH." });
            }

            var ftpOpen = resultData.Connections.Any(c => c.LocalPort == 21 && c.State == "Listen") || resultData.ListeningPorts.Any(c => c.LocalPort == 21);
            if (ftpOpen)
            {
                score -= 10;
                findings.Add(new Finding { Level = Severity.Warning, Title = "Insecure FTP enabled", Description = "FTP port 21 open", Recommendation = "Disable FTP and use SFTP." });
            }

            var ispDns = true;
            foreach (var a in resultData.Adapters)
            {
                if (a.DnsServers.Contains("8.8.8.8") || a.DnsServers.Contains("1.1.1.1") || a.DnsServers.Contains("8.8.4.4") || a.DnsServers.Contains("1.0.0.1"))
                {
                    ispDns = false;
                }
            }
            if (ispDns && resultData.Adapters.Any(a => a.IsConnected))
            {
                score -= 5;
                findings.Add(new Finding { Level = Severity.Info, Title = "ISP DNS in use", Description = "Consider using 1.1.1.1 or 8.8.8.8", Recommendation = "Use secure DNS for better privacy." });
            }

            if (resultData.PingTest.PacketLoss > 2)
            {
                score -= 5;
            }

            if (resultData.Connections.Count > 10)
            {
                findings.Add(new Finding { Level = Severity.Info, Title = $"High number of connections: {resultData.Connections.Count}", Description = "More than 10 active connections", Recommendation = "Review active connections." });
            }

            score = Math.Max(0, score);
            stopwatch.Stop();

            return new ModuleResult
            {
                ModuleName = ModuleName,
                Success = true,
                Score = score,
                Grade = ScoreCalculator.GetGrade(score),
                Findings = findings,
                Data = resultData,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NetworkScanner failed entirely.");
            stopwatch.Stop();
            return new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new NetworkInfo(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tcpTableType, int reserved);

    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] localPort;
        public uint remoteAddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] remotePort;
        public int owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 1)]
        public MIB_TCPROW_OWNER_PID[] table;
    }

    private class TcpRow
    {
        public IPAddress LocalAddress { get; set; } = IPAddress.Any;
        public int LocalPort { get; set; }
        public IPAddress RemoteAddress { get; set; } = IPAddress.Any;
        public int RemotePort { get; set; }
        public TcpState State { get; set; }
        public int ProcessId { get; set; }
    }

    private List<TcpRow> GetAllTcpConnections()
    {
        var tcpRows = new List<TcpRow>();
        int bufferSize = 0;
        
        GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        
        IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
        try
        {
            uint ret = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (ret == 0)
            {
                MIB_TCPTABLE_OWNER_PID table = Marshal.PtrToStructure<MIB_TCPTABLE_OWNER_PID>(tcpTablePtr);
                IntPtr rowPtr = (IntPtr)((long)tcpTablePtr + Marshal.SizeOf(table.dwNumEntries));
                
                for (int i = 0; i < table.dwNumEntries; i++)
                {
                    MIB_TCPROW_OWNER_PID row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                    
                    tcpRows.Add(new TcpRow
                    {
                        State = (TcpState)row.state,
                        LocalAddress = new IPAddress(row.localAddr),
                        LocalPort = BitConverter.ToUInt16(new byte[] { row.localPort[1], row.localPort[0] }, 0),
                        RemoteAddress = new IPAddress(row.remoteAddr),
                        RemotePort = BitConverter.ToUInt16(new byte[] { row.remotePort[1], row.remotePort[0] }, 0),
                        ProcessId = row.owningPid
                    });
                    
                    rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(row));
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }
        
        return tcpRows;
    }
}
