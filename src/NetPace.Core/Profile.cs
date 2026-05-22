namespace NetPace.Core;

/// <summary>
/// Provider-agnostic vocabulary describing the intent of a speed-test run —
/// how much traffic to generate and how aggressively. Each provider's settings
/// record translates these labels into provider-specific values.
/// </summary>
public enum Profile
{
    /// <summary>Typical home broadband (≤ ~100 MiB down + ~21 MiB up per run). Value 0 so <c>default(Profile)</c> resolves here.</summary>
    Medium = 0,

    /// <summary>IoT / 10 MB-month plans (≤ ~245 KB down + ~50 KB up per run).</summary>
    Tiny = 1,

    /// <summary>Cellular / metered (≤ ~10 MiB down + ~2 MiB up per run).</summary>
    Small = 2,

    /// <summary>Fibre / business (≤ ~1 GiB down + ~211 MiB up per run).</summary>
    Large = 3,

    /// <summary>
    /// Inter-DC / 10 Gbps saturation (≤ ~10 GiB down + ~2 GiB up per run).
    /// Uses undocumented OoklaServer payloads (5000/6000/7000) which are not part
    /// of the historic Speedtest.net Flash-client array. May break on future
    /// OoklaServer releases — see docs/architecture/download-upload-size-controls.md.
    /// </summary>
    Mega = 4,
}
