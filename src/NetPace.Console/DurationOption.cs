using System.CommandLine.Parsing;
using System.Globalization;

namespace NetPace.Console;

/// <summary>
/// Reading the value of a duration option: <c>--time-limit</c> and <c>--delay</c>, which take a
/// whole number with a unit, such as <c>30s</c>, <c>10m</c>, <c>2h</c> or <c>1d</c>, or hours,
/// minutes and seconds as <c>HH:MM:SS</c>.
/// </summary>
/// <remarks>
/// Both options read their value through here, so neither can quietly accept a form the other
/// refuses. Nothing wider is accepted: <see cref="TimeSpan"/>'s own parsing reads shorter forms as
/// something else, a bare <c>600</c> as six hundred days and <c>10:00</c> as ten hours, so widening
/// this parser to it would bind a duration nobody asked for. Why the accepted set is drawn where it
/// is, lower-case units and a ceiling included, is in
/// docs/change-intent-records/2026-11-25-a-duration-is-written-one-way.md.
/// </remarks>
internal static class DurationOption
{
    /// <summary>
    /// The duration a refused value is shown as a workable example, in the suffixed form.
    /// </summary>
    public const string Example = "10m";

    /// <summary>
    /// The longest duration either option accepts.
    /// </summary>
    public static readonly TimeSpan Longest = TimeSpan.FromDays(30);

    /// <summary>
    /// The one colon-separated form accepted, exactly hours, minutes and seconds.
    /// </summary>
    private const string ColonFormat = @"hh\:mm\:ss";

    /// <summary>
    /// The units, largest first, as <see cref="Spell"/> needs them to pick the shortest spelling.
    /// </summary>
    private static readonly (long Seconds, char Unit)[] Units =
    [
        (24 * 60 * 60, 'd'),
        (60 * 60, 'h'),
        (60, 'm'),
        (1, 's'),
    ];

    /// <summary>
    /// Digits enough to overflow nothing: any count this long is past <see cref="Longest"/> in
    /// every unit, so it is reported as too long rather than multiplied out first.
    /// </summary>
    private const int MostDigits = 18;

    /// <summary>
    /// Why a duration was refused, which decides what the user is told to write instead.
    /// </summary>
    private enum Problem
    {
        None,
        NeedsAUnit,
        Unreadable,
        TooLong,
        NotMoreThanZero,
    }

    /// <summary>
    /// Reads a duration option's value, reporting an unusable one against
    /// <paramref name="optionName"/> with a workable example.
    /// </summary>
    /// <param name="result">The token the user typed, as the parser is handed it.</param>
    /// <param name="optionName">The option the value was given to, as the message names it.</param>
    /// <param name="allowZero">
    /// Whether a duration of nothing is usable: it is for <c>--delay</c>, whose default it is, and
    /// is not for <c>--time-limit</c>, where it would bound a run to no time at all.
    /// </param>
    /// <remarks>
    /// The value returned after an error is read by nobody: <c>Program.RunAsync</c> returns on a
    /// non-empty <c>ParseResult.Errors</c> before invoking the command.
    /// <para>
    /// Both options take exactly one value, so System.CommandLine's own arity check refuses a
    /// missing one before this parser runs; the missing-token branch guards an option that sets
    /// <c>ArgumentArity.ZeroOrOne</c>, which would otherwise index an empty token list.
    /// </para>
    /// </remarks>
    public static TimeSpan Parse(ArgumentResult result, string optionName, bool allowZero)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Tokens.Count == 0)
        {
            result.AddError($"{optionName} needs a value. {Advice(Problem.Unreadable, string.Empty)}");
            return TimeSpan.Zero;
        }

        string value = result.Tokens[0].Value;
        Problem problem = Read(value, allowZero, out TimeSpan duration);

        if (problem != Problem.None)
        {
            result.AddError($"'{value}' is not a valid value for {optionName}. {Advice(problem, value)}");
        }

        return duration;
    }

    /// <summary>
    /// The duration written as a user would type it, in the largest unit that divides it exactly,
    /// so <c>--help</c> shows a default that can be pasted straight back into the option.
    /// </summary>
    public static string Spell(TimeSpan duration)
    {
        var seconds = (long)duration.TotalSeconds;

        foreach ((long secondsPerUnit, char unit) in Units)
        {
            if (seconds != 0 && seconds % secondsPerUnit == 0)
            {
                return $"{seconds / secondsPerUnit}{unit}";
            }
        }

        return $"{seconds}s";
    }

    /// <summary>
    /// Reads one duration, saying why it is unusable where it is.
    /// </summary>
    private static Problem Read(string value, bool allowZero, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        long secondsPerUnit = value.Length > 1 ? SecondsPerUnit(value[^1]) : 0;

        if (secondsPerUnit > 0 && value[..^1].All(char.IsAsciiDigit))
        {
            string digits = value[..^1].TrimStart('0');

            if (digits.Length > MostDigits)
            {
                return Problem.TooLong;
            }

            long count = digits.Length == 0 ? 0 : long.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

            if (count > (long)Longest.TotalSeconds / secondsPerUnit)
            {
                return Problem.TooLong;
            }

            duration = TimeSpan.FromSeconds(count * secondsPerUnit);
        }
        else if (!TimeSpan.TryParseExact(value, ColonFormat, CultureInfo.InvariantCulture, out duration))
        {
            // Hours, minutes and seconds cannot reach the ceiling, so nothing read that way is
            // ever too long. Digits alone are the near miss worth naming: the user meant a
            // duration and left off the unit.
            return value.Length > 0 && value.All(char.IsAsciiDigit) ? Problem.NeedsAUnit : Problem.Unreadable;
        }

        return duration > TimeSpan.Zero || allowZero ? Problem.None : Problem.NotMoreThanZero;
    }

    /// <summary>
    /// How many seconds one of the unit <paramref name="suffix"/> names is worth, or zero where it
    /// names no unit.
    /// </summary>
    private static long SecondsPerUnit(char suffix)
    {
        foreach ((long seconds, char unit) in Units)
        {
            if (suffix == unit)
            {
                return seconds;
            }
        }

        return 0;
    }

    /// <summary>
    /// What to write instead, named for the way the value given fell short.
    /// </summary>
    private static string Advice(Problem problem, string value) => problem switch
    {
        Problem.NeedsAUnit => $"A duration needs a unit: write it as {value}s for seconds, or {Example} for ten minutes.",
        Problem.TooLong => $"The longest allowed is {Spell(Longest)}.",
        Problem.NotMoreThanZero => $"Give a duration of more than zero, such as {Example}.",
        _ => $"Give a whole number with a unit (s, m, h or d), such as {Example}, or HH:MM:SS, such as 00:10:00.",
    };
}
