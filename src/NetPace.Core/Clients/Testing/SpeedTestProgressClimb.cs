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
    /// Bytes trail the clock throughout, the last report included, so a stub whose last report
    /// already matched the result would hide whether the consumer lands on it at all.
    /// </remarks>
    private static readonly (int PercentageComplete, int BytesPercentage)[] Climb =
    [
        (25, 10),
        (50, 35),
        (75, 65),
        (100, 95),
    ];

    /// <summary>
    /// How many reports go out before the first probe has answered, so a consumer is shown the
    /// blank cell a real transfer starts with.
    /// </summary>
    private const int ReportsBeforeTheFirstProbeAnswers = 1;

    /// <summary>
    /// The reports a measurement raises on its way to <paramref name="result"/>, in order.
    /// </summary>
    /// <param name="result">The result the measurement goes on to return.</param>
    /// <returns>
    /// Four reports of increasing completeness, none of them carrying the result's full byte count.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The latency figures arrive as a real transfer's do: none by the first report, the latency
    /// alone by the second, and the jitter from the third.
    /// <para>
    /// Unlike the bytes they land on the result's own figures, so a consumer is watching the
    /// numbers it will be given.
    /// </para>
    /// </remarks>
    public static IEnumerable<SpeedTestProgress> Towards(SpeedTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var reportsSoFar = 0;

        foreach ((int percentageComplete, int bytesPercentage) in Climb)
        {
            reportsSoFar++;

            // How many probes have answered by this report, which is what decides whether it
            // carries a latency at all and whether it carries the unevenness between a pair.
            var probesAnswered = reportsSoFar - ReportsBeforeTheFirstProbeAnswers;

            yield return new SpeedTestProgress
            {
                PercentageComplete = percentageComplete,
                BytesProcessed = result.BytesProcessed * bytesPercentage / 100,
                ElapsedMilliseconds = result.ElapsedMilliseconds * percentageComplete / 100,
                LatencyMilliseconds = probesAnswered >= 1 ? result.Latency?.LatencyMilliseconds : null,
                JitterMilliseconds = probesAnswered >= 2 ? result.Latency?.JitterMilliseconds : null
            };
        }
    }
}
