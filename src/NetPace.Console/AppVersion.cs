using System.Reflection;

namespace NetPace.Console;

/// <summary>
/// The version NetPace reports about itself, through <c>--version</c> and the diagnostic log.
/// </summary>
/// <remarks>
/// The informational version, because only it can carry a prerelease suffix. Reading an assembly
/// attribute reflects over no type or member, so it is safe to trim and publish AOT.
/// </remarks>
internal static class AppVersion
{
    /// <summary>
    /// The version this build of NetPace was stamped with, prerelease suffix included.
    /// </summary>
    public static string Current { get; } = FromInformationalVersion(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Returns the release name within an informational version, without the source revision the SDK
    /// appends after a <c>+</c>, or <c>"Unknown"</c> when there is no version.
    /// </summary>
    public static string FromInformationalVersion(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion))
        {
            return "Unknown";
        }

        var metadataStart = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return metadataStart < 0 ? informationalVersion : informationalVersion[..metadataStart];
    }
}
