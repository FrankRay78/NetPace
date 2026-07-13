using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// CLI-side helpers that derive measurement validity from a <see cref="SpeedTestResult"/>'s request
/// counts. The counts are the universal currency of validity; these helpers apply the consumer
/// policy the core library deliberately does not.
/// </summary>
internal static class SpeedTestResultReporting
{
    /// <summary>
    /// Whether every request in the dimension failed (zero valid measurement).
    /// </summary>
    public static bool IsAllFailed(this SpeedTestResult result) =>
        result.RequestsAttempted > 0 && result.RequestsSucceeded == 0;

    /// <summary>
    /// Whether at least one request in the dimension failed.
    /// </summary>
    public static bool HasFailures(this SpeedTestResult result) => result.RequestsFailed > 0;

    /// <summary>
    /// The parenthetical annotation appended to a result token when any request failed
    /// (for example, <c>" (5 of 150 requests failed)"</c>); an empty string when none failed.
    /// </summary>
    public static string GetFailureAnnotation(this SpeedTestResult result) =>
        result.RequestsFailed > 0
            ? $" ({result.RequestsFailed} of {result.RequestsAttempted} requests failed)"
            : string.Empty;
}
