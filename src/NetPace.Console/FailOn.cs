namespace NetPace.Console;

/// <summary>
/// Controls whether a measurement outcome causes a non-zero exit code.
/// </summary>
/// <remarks>
/// By default (<see cref="None"/>) network conditions never affect the exit code — they are data,
/// reported via the request counts in the output. The other values let a consumer opt in to
/// treating a failed measurement as a process failure (for example, in CI). Evaluation is
/// fail-fast and uniform across single, <c>--count</c>, and <c>--loop</c> runs.
/// </remarks>
public enum FailOn
{
    /// <summary>
    /// Measurement outcomes never affect the exit code (default).
    /// </summary>
    None,

    /// <summary>
    /// Exit with a non-zero code when a requested dimension is all-failed (no request succeeded).
    /// </summary>
    Total,

    /// <summary>
    /// Exit with a non-zero code when any request in a requested dimension failed.
    /// </summary>
    Partial
}
