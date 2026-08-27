using System.Xml;
using System.Xml.Linq;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Verifies <see cref="Profile.Mega"/>'s XML documentation includes the undocumented-payload
/// caveat, so the warning ships to NuGet consumers via NetPace.Core.xml, and that the loader
/// used to read that file tolerates a build concurrently rewriting it.
/// </summary>
public sealed class ProfileXmlDocTests
{
    private const int LoadAttempts = 5;
    private const int LoadRetryDelayMs = 50;

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
        // Regression (Principle IX mechanism-pinning exception). NetPace.Core.xml is a build-copied
        // artefact sitting in this test project's own output directory, so a build that overlaps the
        // test run can hold it open for writing. XDocument.Load(string) opens with FileShare.Read —
        // a share mode that refuses to coexist with an existing writer — so the open failed with
        // "The process cannot access the file because it is being used by another process", failing
        // the suite intermittently.
        //
        // Only Windows enforces share modes, so only there can this test tell the old load from the
        // new one; that is why the premise is asserted under OperatingSystem.IsWindows() rather than
        // assumed. The outcome is asserted on every platform and nothing is skipped (Principle X),
        // but a Unix-only CI run cannot catch a revert of LoadXmlDoc.

        // Given
        // A private temp file, so the test never contends for the real build artefact — the
        // file-sharing behaviour under test is identical on any path.
        var xmlPath = Path.Join(Path.GetTempPath(), $"netpace-xmldoc-share-{Guid.NewGuid()}.xml");
        File.WriteAllText(xmlPath, "<doc><members /></doc>");

        try
        {
            using var competingWriter = new FileStream(xmlPath, FileMode.Open, FileAccess.Write, FileShare.Read);

            if (OperatingSystem.IsWindows())
            {
                // If this ever stops throwing, the assertion below has quietly stopped proving anything.
                Should.Throw<IOException>(() => XDocument.Load(xmlPath));
            }

            // When
            var doc = LoadXmlDoc(xmlPath);

            // Then
            doc.Root!.Name.LocalName.ShouldBe("doc");
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    /// <summary>
    /// Resolves NetPace.Core.xml, copied next to the referenced NetPace.Core assembly by the
    /// project reference.
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
    /// Loads the XML doc, tolerating a build that is concurrently rewriting it. Two transient
    /// conditions are possible: the open is refused because a writer holds the file, or the file is
    /// observed part-written and fails to parse. Both are retried; a persistent failure still throws.
    /// </summary>
    private static XDocument LoadXmlDoc(string xmlPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return XDocument.Load(stream);
            }
            catch (IOException) when (attempt < LoadAttempts)
            {
                Thread.Sleep(LoadRetryDelayMs);
            }
            catch (XmlException) when (attempt < LoadAttempts)
            {
                Thread.Sleep(LoadRetryDelayMs);
            }
        }
    }

    /// <summary>
    /// Deletes the temp file, guarding only the delete so a cleanup failure cannot replace a real
    /// assertion failure propagating out of the test.
    /// </summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A transient lock on the temp file is not worth failing the test over.
        }
    }
}
