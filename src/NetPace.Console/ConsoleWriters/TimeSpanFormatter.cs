namespace NetPace.Console.ConsoleWriters;

internal static class TimeSpanFormatter
{
    internal static string Humanize(this TimeSpan ts)
    {
        var seconds = (int)Math.Round(ts.TotalSeconds, MidpointRounding.AwayFromZero);
        if (seconds <= 0)
        {
            return "0 seconds";
        }

        return seconds == 1 ? "1 second" : $"{seconds} seconds";
    }
}
