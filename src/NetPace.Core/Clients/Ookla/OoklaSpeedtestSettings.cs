using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using NetPace.Core.Clients.Ookla.Settings;

public sealed record OoklaSpeedtestSettings
{
    // Test-specific settings
    public ServerDiscoverySettings ServerDiscovery { get; init; } = new();
    public LatencyTestSettings LatencyTest { get; init; } = new();
    public DownloadTestSettings DownloadTest { get; init; } = new();
    public UploadTestSettings UploadTest { get; init; } = new();

    // Network options
    public NetworkCredential? ProxyCredential { get; init; }
    public Uri? ProxyAddress { get; init; }
    public bool UseProxy { get; init; }
}
