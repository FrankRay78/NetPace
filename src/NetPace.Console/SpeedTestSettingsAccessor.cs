using NetPace.Core.Clients.Ookla;
using NetPace.Core.Settings;

namespace NetPace.Console;

/// <summary>
/// DI-resolved holder for the <see cref="SpeedTestSettings"/> built from CLI arguments.
/// Set by <see cref="Program.RunAsync"/> after option binding; read by the speed-test service
/// factory, which picks the provider from the kind of settings record it finds here, so there is
/// no separate provider field that could disagree with it.
/// Tests can inspect <see cref="Settings"/> after a run to verify CLI → settings binding.
/// </summary>
public sealed class SpeedTestSettingsAccessor
{
    /// <summary>
    /// The settings to pass into the provider. Default before option binding is an Ookla run on
    /// <see cref="NetPace.Core.Profile.Medium"/> (same as <c>new OoklaSpeedtestSettings()</c>),
    /// which is what <c>netpace servers</c> uses: it never sets this.
    /// </summary>
    public SpeedTestSettings Settings { get; set; } = new OoklaSpeedtestSettings();
}
