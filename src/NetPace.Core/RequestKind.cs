namespace NetPace.Core;

/// <summary>
/// What a reported request was for, so a consumer can tell the requests a test is measuring from
/// the ones it makes alongside them.
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
    /// The empty request made before an upload test to find the address the server wants uploads
    /// sent to. It carries no bytes and is not part of the measured speed.
    /// </summary>
    EndpointProbe,

    /// <summary>
    /// A request for the server's latency endpoint, timing one round trip. Reported by the idle
    /// latency measurement and by server screening, where the probes are the measurement, and
    /// during a download or an upload, where they measure latency under load.
    /// </summary>
    LatencyProbe
}
