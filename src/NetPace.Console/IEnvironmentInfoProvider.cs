using System.Runtime.InteropServices;

namespace NetPace.Console;

/// <summary>
/// Interface for retrieving what NetPace is running as, and on.
/// </summary>
/// <remarks>
/// Diagnostics report these so a bug report identifies the build and the machine without a second
/// round of questions. Stubbed in tests, because every value differs between machines.
/// </remarks>
public interface IEnvironmentInfoProvider
{
    /// <summary>
    /// Returns the NetPace version.
    /// </summary>
    string GetVersion();

    /// <summary>
    /// Returns a description of the .NET runtime NetPace is running on.
    /// </summary>
    string GetRuntime();

    /// <summary>
    /// Returns a description of the operating system NetPace is running on.
    /// </summary>
    string GetOperatingSystem();

    /// <summary>
    /// Returns the process architecture NetPace is running as.
    /// </summary>
    string GetArchitecture();
}

/// <summary>
/// Production implementation of <see cref="IEnvironmentInfoProvider"/>.
/// </summary>
/// <remarks>
/// Every value comes from a BCL API that survives trimming - three <see cref="RuntimeInformation"/>
/// properties and <see cref="System.Reflection.AssemblyName"/> off this assembly's own identity.
/// None of them reflects over types or members, which is what would make the type unsafe to trim
/// or publish AOT, so a fifth value must come from the same kind of source.
/// </remarks>
public sealed class EnvironmentInfoProvider : IEnvironmentInfoProvider
{
    /// <inheritdoc />
    /// <remarks>Returns <c>"Unknown"</c> when the assembly carries no version.</remarks>
    public string GetVersion()
    {
        var assemblyVersion = typeof(EnvironmentInfoProvider).Assembly.GetName().Version;

        return assemblyVersion != null
            ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
            : "Unknown";
    }

    /// <inheritdoc />
    public string GetRuntime() => RuntimeInformation.FrameworkDescription;

    /// <inheritdoc />
    public string GetOperatingSystem() => RuntimeInformation.OSDescription;

    /// <inheritdoc />
    public string GetArchitecture() => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
}

/// <summary>
/// Test stub implementation of <see cref="IEnvironmentInfoProvider"/> returning fixed stand-in
/// values, so a diagnostic snapshot does not change with the machine that ran the suite.
/// </summary>
public sealed class EnvironmentInfoProviderStub : IEnvironmentInfoProvider
{
    /// <summary>Gets or initialises the version returned by <see cref="GetVersion"/>.</summary>
    public string Version { get; init; } = "0.0.0";

    /// <summary>Gets or initialises the runtime returned by <see cref="GetRuntime"/>.</summary>
    public string Runtime { get; init; } = ".NET 10.0.0";

    /// <summary>Gets or initialises the operating system returned by <see cref="GetOperatingSystem"/>.</summary>
    public string OperatingSystem { get; init; } = "Test OS 1.0";

    /// <summary>Gets or initialises the architecture returned by <see cref="GetArchitecture"/>.</summary>
    public string Architecture { get; init; } = "x64";

    /// <inheritdoc />
    public string GetVersion() => Version;

    /// <inheritdoc />
    public string GetRuntime() => Runtime;

    /// <inheritdoc />
    public string GetOperatingSystem() => OperatingSystem;

    /// <inheritdoc />
    public string GetArchitecture() => Architecture;
}
