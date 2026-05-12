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
        // SCENARIO: Parser deserializes a representative Ookla server-list response

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

        var speedtest = BuildSpeedtestWithXmlResponse(Xml);

        var servers = await speedtest.GetServersAsync();

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
        // SCENARIO: Parser populates optional attributes when present

        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers>
                <server id="1" name="London" country="United Kingdom" sponsor="Acme" host="host.example.com:8080" url="http://host.example.com/upload.php" lat="51.5074" lon="-0.1278" />
              </servers>
            </settings>
            """;

        var speedtest = BuildSpeedtestWithXmlResponse(Xml);

        var servers = await speedtest.GetServersAsync();

        var server = servers.ShouldHaveSingleItem().ShouldBeOfType<OoklaServer>();
        server.Country.ShouldBe("United Kingdom");
        server.Host.ShouldBe("host.example.com:8080");
    }

    [Fact]
    public async Task GetServersAsync_OptionalAttributesAbsent_LeavesCountryAndHostNull()
    {
        // SCENARIO: Parser leaves optional attributes null when absent

        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers>
                <server id="1" name="London" sponsor="Acme" url="http://host.example.com/upload.php" lat="51.5074" lon="-0.1278" />
              </servers>
            </settings>
            """;

        var speedtest = BuildSpeedtestWithXmlResponse(Xml);

        var servers = await speedtest.GetServersAsync();

        var server = servers.ShouldHaveSingleItem().ShouldBeOfType<OoklaServer>();
        server.Country.ShouldBeNull();
        server.Host.ShouldBeNull();
    }

    [Fact]
    public async Task GetServersAsync_NumericAttributes_UsesInvariantCultureUnderCommaDecimalLocale()
    {
        // SCENARIO: Parser uses invariant culture for numeric attribute parsing

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
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var speedtest = BuildSpeedtestWithXmlResponse(Xml);

            var servers = await speedtest.GetServersAsync();

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
        // SCENARIO: Parser handles an empty servers element

        const string Xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <settings>
              <servers></servers>
            </settings>
            """;

        var speedtest = BuildSpeedtestWithXmlResponse(Xml);

        var servers = await speedtest.GetServersAsync();

        servers.ShouldBeEmpty();
    }

    private static OoklaSpeedtest BuildSpeedtestWithXmlResponse(string xml)
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("application/xml", xml);
        return new OoklaSpeedtest(httpClientOverride: mockHttp.ToHttpClient());
    }
}
