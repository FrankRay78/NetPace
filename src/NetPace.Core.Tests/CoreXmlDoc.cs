using System.Xml.Linq;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Reads <c>NetPace.Core.xml</c> — the documentation file that ships to NuGet consumers — so a
/// test can assert what a public member's documentation actually tells them.
/// </summary>
internal static class CoreXmlDoc
{
    /// <summary>
    /// Loads the documentation file built alongside <c>NetPace.Core.dll</c>.
    /// </summary>
    public static XDocument Load() => Load(ResolvePath());

    /// <summary>
    /// The <c>member</c> elements whose documented name starts with <paramref name="namePrefix"/> —
    /// every overload of a method, where the prefix names the method.
    /// </summary>
    public static XElement[] MembersStartingWith(this XDocument doc, string namePrefix) =>
        [.. doc.Descendants("member").Where(member => ((string?)member.Attribute("name"))?.StartsWith(namePrefix, StringComparison.Ordinal) == true)];

    /// <summary>
    /// The types a member's documentation says it throws, by the <c>cref</c> each
    /// <c>&lt;exception&gt;</c> element names.
    /// </summary>
    public static string[] DocumentedExceptions(this XElement member) =>
        [.. member.Elements("exception").Select(element => (string?)element.Attribute("cref") ?? string.Empty)];

    private static string ResolvePath()
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
    internal static XDocument Load(string xmlPath)
    {
        using var stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return XDocument.Load(stream);
    }
}
