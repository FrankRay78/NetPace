using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetPace.Console;

/// <summary>
/// Interface for retrieving local device identity information.
/// </summary>
public interface IClientInfoProvider
{
    /// <summary>
    /// Returns the device's first available IPv4 address; falls back to the first available IPv6
    /// address; returns an empty string if no unicast addresses are found; returns <c>"ERROR"</c>
    /// if an exception occurs during retrieval.
    /// </summary>
    string GetIPAddress();

    /// <summary>
    /// Returns the device hostname as reported by the OS; returns an empty string if the hostname
    /// resolves to empty; returns <c>"ERROR"</c> if an exception occurs during retrieval.
    /// </summary>
    string GetHostname();
}

/// <summary>
/// Production implementation of <see cref="IClientInfoProvider"/> using BCL networking APIs.
/// </summary>
/// <remarks>
/// Neither method ever throws — all exceptions are caught internally and a safe string value is returned.
/// </remarks>
public sealed class ClientInfoProvider : IClientInfoProvider
{
    /// <inheritdoc />
    public string GetIPAddress()
    {
        try
        {
            string? ipv6Address = null;

            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var unicast in iface.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        return unicast.Address.ToString();

                    if (ipv6Address is null && unicast.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        ipv6Address = unicast.Address.ToString();
                }
            }

            return ipv6Address ?? string.Empty;
        }
        catch
        {
            return "ERROR";
        }
    }

    /// <inheritdoc />
    public string GetHostname()
    {
        try
        {
            var hostname = Dns.GetHostName();
            return string.IsNullOrEmpty(hostname) ? string.Empty : hostname;
        }
        catch
        {
            return "ERROR";
        }
    }
}

/// <summary>
/// Test stub implementation of <see cref="IClientInfoProvider"/> that returns deterministic values.
/// </summary>
/// <remarks>
/// Configure <see cref="IPAddress"/> and <see cref="Hostname"/> via <c>init</c> setters to simulate
/// different device identity scenarios in unit tests.
/// </remarks>
public sealed class ClientInfoProviderStub : IClientInfoProvider
{
    /// <summary>Gets or initialises the IP address returned by <see cref="GetIPAddress"/>.</summary>
    public string IPAddress { get; init; } = "192.168.1.1";

    /// <summary>Gets or initialises the hostname returned by <see cref="GetHostname"/>.</summary>
    public string Hostname { get; init; } = "test-host";

    /// <inheritdoc />
    public string GetIPAddress() => IPAddress;

    /// <inheritdoc />
    public string GetHostname() => Hostname;
}

/// <summary>
/// Test stub that returns <c>"ERROR"</c> for both methods, simulating what
/// <see cref="ClientInfoProvider"/> returns when its internal exception handling fires.
/// </summary>
public sealed class ClientInfoProviderErrorStub : IClientInfoProvider
{
    /// <inheritdoc />
    public string GetIPAddress() => "ERROR";

    /// <inheritdoc />
    public string GetHostname() => "ERROR";
}
