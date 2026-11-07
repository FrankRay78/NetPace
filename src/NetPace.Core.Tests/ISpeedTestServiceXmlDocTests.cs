using System.Xml.Linq;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Verifies that the documentation shipped to NuGet consumers names the exception a screening call
/// can raise, so a consumer reading it knows which type to catch rather than being pointed at
/// <see cref="Exception"/>, which cannot be caught selectively.
/// </summary>
public sealed class ISpeedTestServiceXmlDocTests
{
    private const string ScreeningMethod = "M:NetPace.Core.ISpeedTestService.GetFastestServerByLatencyAsync";

    [Fact]
    public void GetFastestServerByLatencyAsync_XmlDoc_NamesTheNoServersExceptionRatherThanTheBaseException()
    {
        // SCENARIO: Library caller catches no servers without a catch-all

        // Given the documentation file built alongside NetPace.Core.dll.
        var doc = CoreXmlDoc.Load();

        // When every documented overload of the screening call is read.
        var overloads = doc.MembersStartingWith(ScreeningMethod);

        // Then each one tells the consumer to catch the no-servers type, and none of them points at
        // the base exception any more.
        overloads.ShouldNotBeEmpty($"XML doc for {ScreeningMethod} is missing — every public member must be documented.");

        foreach (XElement overload in overloads)
        {
            var documented = overload.DocumentedExceptions();
            var whatItDocuments = $"{(string?)overload.Attribute("name")} documents {string.Join(", ", documented)}";

            documented.ShouldContain("T:NetPace.Core.NoServersAvailableException", whatItDocuments);
            documented.ShouldNotContain("T:System.Exception", whatItDocuments);
        }
    }
}
