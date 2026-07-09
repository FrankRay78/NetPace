using NetPace.Core.Clients.Ookla;

namespace NetPace.Console;

/// <summary>
/// DI-resolved holder for the <see cref="OoklaSpeedtestSettings"/> built from CLI arguments.
/// Set by <see cref="Program.RunAsync"/> after option binding; read by the
/// <see cref="OoklaSpeedtest"/> factory when the production speed-test service is resolved.
/// Tests can inspect <see cref="Settings"/> after a run to verify CLI → settings binding.
/// </summary>
public sealed class OoklaSpeedtestSettingsAccessor
{
    /// <summary>
    /// The settings to pass into <see cref="OoklaSpeedtest"/>. Default before option binding
    /// is <see cref="Profile.Medium"/> (same as <c>new OoklaSpeedtestSettings()</c>).
    /// </summary>
    public OoklaSpeedtestSettings Settings { get; set; } = new();
}
