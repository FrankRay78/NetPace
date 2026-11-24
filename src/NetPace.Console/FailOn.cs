namespace NetPace.Console;

/// <summary>
/// Controls whether a measurement outcome causes a non-zero exit code.
/// </summary>
/// <remarks>
/// A network condition is data by default, reported through the request counts; the other values
/// opt in to treating a failed measurement as a process failure. Fail-fast, and uniform.
/// </remarks>
public enum FailOn
{
    /// <summary>
    /// Measurement outcomes never affect the exit code (default).
    /// </summary>
    None,

    /// <summary>
    /// Exit with a non-zero code when a requested test is all-failed (no request succeeded).
    /// </summary>
    Total,

    /// <summary>
    /// Exit with a non-zero code when any request in a requested test failed.
    /// </summary>
    Partial
}
