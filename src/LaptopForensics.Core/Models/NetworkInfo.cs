namespace LaptopForensics.Core.Models;

public class NetworkInfo
{
    public List<NetworkAdapter> Adapters { get; set; } = new();
    public List<TcpConnectionInfo> Connections { get; set; } = new();
    public List<UdpPortInfo> ListeningPorts { get; set; } = new();
    public List<SavedWifiNetwork> SavedNetworks { get; set; } = new();
    public PingResult PingTest { get; set; } = new();
    public int FirewallRuleCount { get; set; }
    public List<string> SuspiciousConnections { get; set; } = new();
}

public class NetworkAdapter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public List<string> IpAddresses { get; set; } = new();
    public string SubnetMask { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public List<string> DnsServers { get; set; } = new();
    public bool DhcpEnabled { get; set; }
    public long SpeedMbps { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public string? ConnectedSsid { get; set; }
}

public class TcpConnectionInfo
{
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string State { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public int? Pid { get; set; }
    public string? RemoteHostname { get; set; }
    public bool IsSuspicious { get; set; }
}

public class UdpPortInfo
{
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string? ProcessName { get; set; }
    public int? Pid { get; set; }
}

public class SavedWifiNetwork
{
    public string Name { get; set; } = string.Empty;
    public string Security { get; set; } = string.Empty;
}

public class PingResult
{
    public double AverageMs { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public int PacketLoss { get; set; }
}
