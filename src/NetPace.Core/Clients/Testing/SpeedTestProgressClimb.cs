namespace NetPace.Core.Clients.Testing;

/// <summary>
/// The progress reports a stubbed measurement raises on its way to a result.
/// </summary>
/// <remarks>
/// Shared by every test double that wants believable progress, so the shape of a stubbed climb is
/// defined once rather than per stub. Each caller keeps its own pacing: the sequence says what to
/// report, not when.
/// </remarks>
public static class SpeedTestProgressClimb
{
    /// <summary>
    /// The percentage complete of each report, paired with the percentage of the result's bytes
    /// that has arrived by then.
    /// </summary>
    /// <remarks>
    /// Bytes trail the clock throughout, and the last report still trails it, so a consumer
    /// averaging since the test began watches the figure climb without ever reaching the result's
    /// own speed. Landing on that speed is the job of whatever the consumer does when the
    /// measurement returns, and a stub whose last report already matched the result would hide
    /// whether the consumer does it at all.
    /// </remarks>
    private static readonly (int PercentageComplete, int BytesPercentage)[] Climb =
    [
        (25, 10),
        (50, 35),
        (75, 65),
        (100, 95),
    ];

    /// <summary>
    /// The reports a measurement raises on its way to <paramref name="result"/>, in order.
    /// </summary>
    /// <param name="result">The result the measurement goes on to return.</param>
    /// <returns>
    /// Four reports of increasing completeness, none of them carrying the result's full byte count.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    public static IEnumerable<SpeedTestProgress> Towards(SpeedTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach ((int percentageComplete, int bytesPercentage) in Climb)
        {
            yield return new SpeedTestProgress
            {
                PercentageComplete = percentageComplete,
                BytesProcessed = result.BytesProcessed * bytesPercentage / 100,
                ElapsedMilliseconds = result.ElapsedMilliseconds * percentageComplete / 100
            };
        }
    }
}
