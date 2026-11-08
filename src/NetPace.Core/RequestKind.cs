namespace NetPace.Core;

/// <summary>
/// What a reported request was for. Which kinds a given test is measuring depends on the test: the
/// latency probes are the measurement in a latency or screening pass, and incidental during a
/// download or an upload.
/// </summary>
/// <remarks>
/// A download or an upload reports requests of more than one kind: the transfers it measures, and —
/// where the measurement is on — the small latency probes it sends while they run. Both are real
/// requests a triager wants to see; only the first is the measurement.
/// </remarks>
public enum RequestKind
{
    /// <summary>
    /// A request moving the test's own data: one download, or one upload. Its bytes and its duration
    /// are part of the measured speed.
    /// </summary>
    Transfer,

    /// <summary>
    /// An empty request issued at the start of an upload test to find the address the server wants
    /// uploads sent to - one per redirect followed. It carries no bytes and is not part of the
    /// measured speed.
    /// </summary>
    EndpointProbe,

    /// <summary>
    /// A request for the server's latency endpoint, timing one round trip. Reported by the idle
    /// latency measurement and by server screening, where the probes are the measurement, and
    /// during a download or an upload, where they measure latency under load.
    /// </summary>
    LatencyProbe
}
