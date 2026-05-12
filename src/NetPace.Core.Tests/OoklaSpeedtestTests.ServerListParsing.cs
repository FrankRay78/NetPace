using System.Globalization;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    [Fact]
    public async Task GetServersAsync_RepresentativeOoklaResponse_ReturnsAllRequiredAttributes()
    {
        // Given
        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers>
                <server id="1234" name="London" country="United Kingdom" sponsor="Acme ISP" host="speedtest.example.com:8080" url="http://speedtest.example.com/speedtest/upload.php" lat="51.5074" lon="-0.1278" />
                <server id="5678" name="New York" country="United States" sponsor="Beta Networks" host="speedtest.ny.example.net:8080" url="http://speedtest.ny.example.net/speedtest/upload.php" lat="40.7128" lon="-74.0060" />
                <server id="9012" name="Tokyo" sponsor="Gamma Telecom" url="http://speedtest.tokyo.example.jp/speedtest/upload.php" lat="35.6762" lon="139.6503" />
                <server id="3456" name="Sydney" country="Australia" sponsor="Delta Communications" host="speedtest.sy.example.au:8080" url="http://speedtest.sy.example.au/speedtest/upload.php" lat="-33.8688" lon="151.2093" />
                <server id="7890" name="Cape Town" sponsor="Epsilon Mobile" url="http://speedtest.cpt.example.za/speedtest/upload.php" lat="-33.9249" lon="18.4241" />
              </servers>
            </settings>
            """;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("application/xml", Xml);
        var speedtest = new OoklaSpeedtest(httpClientOverride: mockHttp.ToHttpClient());

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        servers.Length.ShouldBe(5);

        var first = servers[0].ShouldBeOfType<OoklaServer>();
        first.Id.ShouldBe(1234);
        first.Location.ShouldBe("London");
        first.Sponsor.ShouldBe("Acme ISP");
        first.Url.ShouldBe("http://speedtest.example.com/speedtest/upload.php");
        first.Latitude.ShouldBe(51.5074);
        first.Longitude.ShouldBe(-0.1278);
    }

    [Fact]
    public async Task GetServersAsync_OptionalAttributesPresent_PopulatesCountryAndHost()
    {
        // Given
        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers>
                <server id="1" name="London" country="United Kingdom" sponsor="Acme" host="host.example.com:8080" url="http://host.example.com/upload.php" lat="51.5074" lon="-0.1278" />
              </servers>
            </settings>
            """;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("application/xml", Xml);
        var speedtest = new OoklaSpeedtest(httpClientOverride: mockHttp.ToHttpClient());

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        var server = servers.ShouldHaveSingleItem().ShouldBeOfType<OoklaServer>();
        server.Country.ShouldBe("United Kingdom");
        server.Host.ShouldBe("host.example.com:8080");
    }

    [Fact]
    public async Task GetServersAsync_OptionalAttributesAbsent_LeavesCountryAndHostNull()
    {
        // Given
        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers>
                <server id="1" name="London" sponsor="Acme" url="http://host.example.com/upload.php" lat="51.5074" lon="-0.1278" />
              </servers>
            </settings>
            """;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("application/xml", Xml);
        var speedtest = new OoklaSpeedtest(httpClientOverride: mockHttp.ToHttpClient());

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        var server = servers.ShouldHaveSingleItem().ShouldBeOfType<OoklaServer>();
        server.Country.ShouldBeNull();
        server.Host.ShouldBeNull();
    }

    [Fact]
    public async Task GetServersAsync_NumericAttributes_UsesInvariantCultureUnderCommaDecimalLocale()
    {
        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers>
                <server id="1" name="London" sponsor="Acme" url="http://host.example.com/upload.php" lat="51.5074" lon="-0.1278" />
              </servers>
            </settings>
            """;

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Given
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond("application/xml", Xml);
            var speedtest = new OoklaSpeedtest(httpClientOverride: mockHttp.ToHttpClient());

            // When
            var servers = await speedtest.GetServersAsync();

            // Then
            var server = servers.ShouldHaveSingleItem().ShouldBeOfType<OoklaServer>();
            server.Latitude.ShouldBe(51.5074);
            server.Longitude.ShouldBe(-0.1278);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task GetServersAsync_EmptyServersElement_ReturnsEmptyCollection()
    {
        // Given
        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers></servers>
            </settings>
            """;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("application/xml", Xml);
        var speedtest = new OoklaSpeedtest(httpClientOverride: mockHttp.ToHttpClient());

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        servers.ShouldBeEmpty();
    }
}
