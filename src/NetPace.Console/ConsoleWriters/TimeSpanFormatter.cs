namespace NetPace.Console.ConsoleWriters;

internal static class TimeSpanFormatter
{
    // Gregorian calendar year, matches Humanizer's constants
    private const double DaysInAYear = 365.2425;
    private const double DaysInAMonth = DaysInAYear / 12;
    private const int DaysInAWeek = 7;

    internal static string Humanize(this TimeSpan ts)
    {
        if (ts <= TimeSpan.Zero) return "0 seconds";

        var totalDays = ts.TotalDays;

        if (totalDays >= DaysInAYear) return Format((int)(totalDays / DaysInAYear), "year", "years");
        if (totalDays >= DaysInAMonth) return Format((int)(totalDays / DaysInAMonth), "month", "months");
        if (totalDays >= DaysInAWeek) return Format((int)(totalDays / DaysInAWeek), "week", "weeks");
        if (totalDays >= 1) return Format((int)totalDays, "day", "days");
        if (ts.TotalHours >= 1) return Format((int)ts.TotalHours, "hour", "hours");
        if (ts.TotalMinutes >= 1) return Format((int)ts.TotalMinutes, "minute", "minutes");
        if (ts.TotalSeconds >= 1) return Format((int)ts.TotalSeconds, "second", "seconds");

        return Format((int)ts.TotalMilliseconds, "millisecond", "milliseconds");
    }

    private static string Format(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural}";
}
