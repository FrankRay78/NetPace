using System.Xml.Linq;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Verifies <see cref="Profile.Mega"/>'s XML documentation includes the undocumented-payload
/// caveat, so the warning ships to NuGet consumers via NetPace.Core.xml.
/// </summary>
public sealed class ProfileXmlDocTests
{
    [Fact]
    public void Profile_Mega_XmlDoc_DocumentsBonusPayloadDependency()
    {
        // SCENARIO: Mega's bonus-payload dependency is documented

        // Given
        var xmlPath = ResolveCoreXmlDocPath();

        // When
        var doc = LoadXmlDoc(xmlPath);
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

    [Fact]
    public void LoadXmlDoc_WhenAnotherProcessHoldsTheFileOpenForWriting_StillLoadsTheDocument()
    {
        // SCENARIO: XML doc loads while a concurrent writer holds the file
        // Regression: a solution-level `dotnet test ./src` copies NetPace.Core.xml into both test
        // projects' output directories, so the file can be held open for writing when this suite
        // runs. XDocument.Load(string) opens with FileShare.Read, which denies that writer and
        // throws IOException, failing the suite intermittently.

        // Given
        // A private temp file, so the test never contends for the real build artefact — the
        // file-sharing behaviour under test is identical either way.
        var xmlPath = Path.Join(Path.GetTempPath(), $"netpace-xmldoc-share-{Guid.NewGuid()}.xml");
        File.WriteAllText(xmlPath, "<doc><members /></doc>");
        try
        {
            using var competingWriter = new FileStream(xmlPath, FileMode.Open, FileAccess.Write, FileShare.Read);

            // When
            var doc = LoadXmlDoc(xmlPath);

            // Then
            doc.Root!.Name.LocalName.ShouldBe("doc");
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    /// <summary>
    /// Resolves NetPace.Core.xml, which MSBuild emits alongside the loaded NetPace.Core assembly.
    /// </summary>
    private static string ResolveCoreXmlDocPath()
    {
        var assemblyPath = typeof(Profile).Assembly.Location;
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");
        File.Exists(xmlPath).ShouldBeTrue(
            $"Expected NetPace.Core.xml next to assembly at '{xmlPath}'. Ensure <GenerateDocumentationFile>true</GenerateDocumentationFile> is set on NetPace.Core.csproj.");
        return xmlPath;
    }

    /// <summary>
    /// Loads the XML doc tolerating a concurrent writer. <c>XDocument.Load(string)</c> opens with
    /// <see cref="FileShare.Read"/>, which denies any process holding the file open for writing.
    /// </summary>
    private static XDocument LoadXmlDoc(string xmlPath)
    {
        using var stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return XDocument.Load(stream);
    }
}
