using System.Globalization;
using NetPace.Console.Commands;
using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// CLI-side helpers that derive measurement validity from a <see cref="SpeedTestResult"/>'s request
/// counts, and render the figure the line-based formats carry. The counts are the universal
/// currency of validity; these helpers apply the consumer policy the core library deliberately does
/// not.
/// </summary>
internal static class SpeedTestResultExtensions
{
    /// <summary>
    /// Whether every request in the test failed (zero valid measurement).
    /// </summary>
    public static bool IsAllFailed(this SpeedTestResult result) =>
        result.RequestsFailed > 0 && result.RequestsSucceeded == 0;

    /// <summary>
    /// Whether at least one request in the test failed.
    /// </summary>
    public static bool HasFailures(this SpeedTestResult result) => result.RequestsFailed > 0;

    /// <summary>
    /// The parenthetical annotation appended to a figure when any request failed (for example,
    /// <c>" (5 of 150 requests failed)"</c>); an empty string when none failed.
    /// </summary>
    public static string GetFailureAnnotation(this SpeedTestResult result) =>
        result.HasFailures()
            ? $" ({result.RequestsFailed.ToString(CultureInfo.InvariantCulture)} of {(result.RequestsSucceeded + result.RequestsFailed).ToString(CultureInfo.InvariantCulture)} requests failed)"
            : string.Empty;

    /// <summary>
    /// A measurement as <c>--minimal</c> and <c>--simple</c> carry it: the speed in the unit the
    /// switches ask for, followed by the failed-request annotation where there is one. Default
    /// output does not use this: its table has a <c>Failed</c> column of its own.
    /// </summary>
    /// <remarks>
    /// One expression for both, so they cannot drift apart in how they render the same
    /// measurement.
    /// </remarks>
    public static string GetFigure(this SpeedTestResult result, SpeedTestCommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);

        return $"{result.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{result.GetFailureAnnotation()}";
    }
}
