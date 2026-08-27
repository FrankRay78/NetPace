using System.Xml.Linq;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Verifies <see cref="Profile.Mega"/>'s XML documentation includes the undocumented-payload
/// caveat, so the warning ships to NuGet consumers via NetPace.Core.xml, and that the loader
/// used to read that file tolerates a concurrent writer.
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
    public void LoadXmlDoc_WhileAConcurrentWriterHoldsTheFile_StillLoadsTheDocument()
    {
        // SCENARIO: XML doc loads while a concurrent writer holds the file
        //
        // Regression: NetPace.Core.xml is a build-copied artefact in this test project's own output
        // directory, so a build overlapping the test run can hold it open for writing.
        // XDocument.Load(string) opens with FileShare.Read, a share mode that refuses to coexist
        // with an existing writer, so the read failed intermittently with "The process cannot
        // access the file because it is being used by another process". Only Windows enforces share
        // modes, so the premise is asserted there rather than assumed — on Unix this test still runs
        // and still asserts the outcome, but cannot catch a revert of LoadXmlDoc.

        // Given
        // A private temp file, so the test never contends for the real build artefact.
        var xmlPath = Path.Join(Path.GetTempPath(), $"netpace-xmldoc-share-{Guid.NewGuid()}.xml");
        File.WriteAllText(xmlPath, "<doc><members /></doc>");

        try
        {
            using var competingWriter = new FileStream(xmlPath, FileMode.Open, FileAccess.Write, FileShare.Read);

            if (OperatingSystem.IsWindows())
            {
                Should.Throw<IOException>(() => XDocument.Load(xmlPath));
            }

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

    private static string ResolveCoreXmlDocPath()
    {
        var assemblyPath = typeof(Profile).Assembly.Location;
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");
        File.Exists(xmlPath).ShouldBeTrue(
            $"Expected NetPace.Core.xml next to assembly at '{xmlPath}'. Ensure <GenerateDocumentationFile>true</GenerateDocumentationFile> is set on NetPace.Core.csproj.");
        return xmlPath;
    }

    /// <summary>
    /// Opens with <see cref="FileShare.ReadWrite"/> so a concurrent writer cannot fail the read;
    /// <c>XDocument.Load(string)</c> opens with <see cref="FileShare.Read"/> and throws instead.
    /// </summary>
    private static XDocument LoadXmlDoc(string xmlPath)
    {
        using var stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return XDocument.Load(stream);
    }
}
