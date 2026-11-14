namespace NetPace.Core;

/// <summary>
/// Provider-agnostic vocabulary describing the intent of a speed-test run —
/// how much traffic to generate and how aggressively. Each provider's settings
/// record translates these labels into provider-specific values.
/// </summary>
/// <remarks>
/// The figures below are each profile's data ceilings, which bind every provider. What a run
/// actually moves inside them depends on the provider's own request sizes, so two providers on the
/// same profile move slightly different amounts under the same ceiling.
/// </remarks>
public enum Profile
{
    /// <summary>Typical home broadband (≤ 100 MiB down + 25 MiB up per run). Value 0 so <c>default(Profile)</c> resolves here.</summary>
    Medium = 0,

    /// <summary>IoT / 10 MB-month plans (≤ 1 MiB down + 1 MiB up per run).</summary>
    Tiny = 1,

    /// <summary>Cellular / metered (≤ 10 MiB down + 2 MiB up per run).</summary>
    Small = 2,

    /// <summary>Fibre / business (≤ 1 GiB down + 256 MiB up per run).</summary>
    Large = 3,

    /// <summary>
    /// Inter-DC / 10 Gbps saturation (≤ 10 GiB down + 2 GiB up per run).
    /// On an Ookla run, uses undocumented OoklaServer payloads (5000/6000/7000) which are
    /// not part of the historic Speedtest.net Flash-client array. May break on future
    /// OoklaServer releases — see docs/architecture/download-upload-size-controls.md.
    /// </summary>
    Mega = 4,
}
