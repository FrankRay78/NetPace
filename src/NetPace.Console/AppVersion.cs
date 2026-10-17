using System.Reflection;

namespace NetPace.Console;

/// <summary>
/// The version NetPace reports about itself, through <c>--version</c> and the diagnostic log.
/// </summary>
/// <remarks>
/// Read from the informational version rather than the assembly version, because only the
/// informational version can carry a prerelease suffix: the assembly version accepts numbers alone,
/// so a <c>0.27.0-rc1</c> build carries assembly version <c>0.27.0</c>. Reading an assembly attribute
/// does not reflect over types or members, so it is safe to trim and publish AOT.
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
