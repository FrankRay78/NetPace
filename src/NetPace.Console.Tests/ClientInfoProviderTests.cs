namespace NetPace.Console.Tests;

public sealed class ClientInfoProviderTests
{
    [Fact]
    public void GetIPAddress_ReturnsNonNullNonErrorString()
    {
        // SCENARIO: First available IPv4 address is returned when device has IPv4 interfaces

        // Given
        var provider = new ClientInfoProvider();

        // When
        var result = provider.GetIPAddress();

        // Then
        Assert.NotNull(result);
        Assert.NotEqual("ERROR", result);
    }

    [Fact]
    public void GetHostname_ReturnsNonNullNonErrorString()
    {
        // SCENARIO: Device hostname is returned when the OS provides one

        // Given
        var provider = new ClientInfoProvider();

        // When
        var result = provider.GetHostname();

        // Then
        Assert.NotNull(result);
        Assert.NotEqual("ERROR", result);
    }
}
