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
    private const string Example = "10m";

    /// <summary>
    /// The longest duration either option accepts, in the suffixed form.
    /// </summary>
    /// <remarks>
    /// A run is bounded by a <see cref="CancellationTokenSource"/> and a delay by
    /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/>, both of which refuse anything past
    /// about 49.7 days, so raising this past that would throw where a run starts.
    /// </remarks>
    internal static readonly TimeSpan Longest = TimeSpan.FromDays(30);

    /// <summary>
    /// The one colon-separated form accepted, exactly hours, minutes and seconds.
    /// </summary>
    private const string ColonFormat = @"hh\:mm\:ss";

    /// <summary>
    /// The longest the colon form can express, which <c>hh</c> holds to two digits and so stops
    /// short of <see cref="Longest"/>.
    /// </summary>
    private static readonly TimeSpan LongestColonForm = new(23, 59, 59);

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
    /// The forms a duration may be written in, as the help text of both options names them, so
    /// neither can describe a set this parser does not read. Each form carries its own ceiling,
    /// because the colon form cannot reach <see cref="Longest"/>.
    /// </summary>
    public static string Forms =>
        $"30s, 10m, 2h or 1d up to {Spell(Longest)}, or HH:MM:SS up to {ColonForm(LongestColonForm)}";

    /// <summary>
    /// Reads a duration option's value, reporting an unusable one against
    /// <paramref name="optionName"/> with a workable example.
    /// </summary>
    /// <param name="result">The argument result the parser is handed, holding the token typed.</param>
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
            result.AddError($"{optionName} needs a value. {Advice(Problem.Unreadable, string.Empty, allowZero)}");
            return Unusable(allowZero);
        }

        string value = result.Tokens[0].Value;
        Problem problem = Read(value, allowZero, out TimeSpan duration);

        if (problem != Problem.None)
        {
            result.AddError($"'{value}' is not a valid value for {optionName}. {Advice(problem, value, allowZero)}");
            return Unusable(allowZero);
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

    private static Problem Read(string value, bool allowZero, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        long secondsPerUnit = value.Length > 1 ? SecondsPerUnit(value[^1]) : 0;

        if (secondsPerUnit > 0 && value[..^1].All(char.IsAsciiDigit))
        {
            // A count of digits alone that will not fit a long is past the ceiling in every unit.
            if (!long.TryParse(value[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out long count)
                || count > (long)Longest.TotalSeconds / secondsPerUnit)
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
    /// The duration written in the colon form, culture independent.
    /// </summary>
    private static string ColonForm(TimeSpan duration) =>
        duration.ToString(ColonFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// What a refused value binds to, which only a caller that ignored the reported error can
    /// reach: never zero where zero is itself refused, since that would cut a run short instead.
    /// </summary>
    private static TimeSpan Unusable(bool allowZero) => allowZero ? TimeSpan.Zero : Longest;

    /// <summary>
    /// What to write instead, named for the way the value given fell short.
    /// </summary>
    /// <remarks>
    /// The missing-unit advice names the user's own number back only once that value reads
    /// cleanly, so the suggestion is never one this same parser would refuse in turn.
    /// </remarks>
    private static string Advice(Problem problem, string value, bool allowZero) => problem switch
    {
        Problem.NeedsAUnit when Read($"{value}s", allowZero, out _) == Problem.None =>
            $"A duration needs a unit: write it as {value}s for seconds, or {Example} for ten minutes.",
        Problem.NeedsAUnit => $"A duration needs a unit, such as {Example} for ten minutes.",
        Problem.TooLong => $"The longest allowed is {Spell(Longest)}.",
        Problem.NotMoreThanZero => $"Give a duration of more than zero, such as {Example}.",
        _ => $"Give a whole number with a unit (s, m, h or d), such as {Example}, or HH:MM:SS up to {ColonForm(LongestColonForm)}.",
    };

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
}
