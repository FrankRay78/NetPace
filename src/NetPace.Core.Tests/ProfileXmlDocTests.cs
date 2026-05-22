using System.Xml.Linq;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Verifies <see cref="Profile.Mega"/>'s XML documentation includes the undocumented-payload
/// caveat (FR-021), so the warning ships to NuGet consumers via NetPace.Core.xml.
/// </summary>
public sealed class ProfileXmlDocTests
{
    [Fact]
    public void Profile_Mega_XmlDoc_DocumentsBonusPayloadDependency()
    {
        // SCENARIO: Mega's bonus-payload dependency is documented

        // Given
        var assemblyPath = typeof(Profile).Assembly.Location;
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");
        File.Exists(xmlPath).ShouldBeTrue(
            $"Expected NetPace.Core.xml next to assembly at '{xmlPath}'. Ensure <GenerateDocumentationFile>true</GenerateDocumentationFile> is set on NetPace.Core.csproj.");

        // When
        var doc = XDocument.Load(xmlPath);
        var memberNode = doc.Descendants("member")
            .FirstOrDefault(m => (string?)m.Attribute("name") == "F:NetPace.Core.Profile.Mega");

        // Then
        memberNode.ShouldNotBeNull("XML doc for Profile.Mega is missing — every public member must be documented.");
        var summary = memberNode!.Element("summary")?.Value ?? string.Empty;

        summary.ShouldContain("undocumented", Case.Insensitive);
        summary.ShouldContain("5000");
        summary.ShouldContain("6000");
        summary.ShouldContain("7000");
        summary.ShouldContain("download-upload-size-controls");
    }
}
