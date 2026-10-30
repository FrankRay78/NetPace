using System.CommandLine.Parsing;

namespace NetPace.Console;

/// <summary>
/// The spellings a user may type for one option with a fixed set of choices, and the value each
/// spelling binds to. One instance answers all three questions about that option's values — how
/// the help lists them, which value a typed token means, and what to say when the token is not one
/// of them — so the three cannot drift apart.
/// </summary>
/// <typeparam name="T">The enumeration the option binds to.</typeparam>
/// <param name="choices">
/// The choices, in the order the help lists them — which need not be the order the enumeration
/// declares its members in.
/// </param>
internal sealed class OptionValueSet<T>(params (string Spelling, T Value)[] choices) where T : struct, Enum
{
    /// <summary>
    /// The choices as the help lists them, for example <c>&lt;bits, bytes&gt;</c>.
    /// </summary>
    public string HelpList => $"<{string.Join(", ", choices.Select(choice => choice.Spelling))}>";

    /// <summary>
    /// The spelling the help uses for <paramref name="value"/>, falling back to the enumeration
    /// member's own name for a value the set does not list, so the DEFAULT column renders something
    /// rather than nothing. The fallback is not a safety net: an unlisted member is absent from
    /// <see cref="HelpList"/> and refused by <see cref="Parse"/>, which is what the tests cover.
    /// </summary>
    public string Spell(T value)
    {
        foreach ((string spelling, T candidate) in choices)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, value))
            {
                return spelling;
            }
        }

        return value.ToString();
    }

    /// <summary>
    /// Bind the token the user typed, in any case. A token that is not one of the choices, or a
    /// missing token where the option allows its value to be omitted, is reported against
    /// <paramref name="optionName"/>, listing the valid values exactly as the help lists them.
    /// </summary>
    /// <remarks>
    /// No option NetPace ships allows its value to be omitted, so for those System.CommandLine's
    /// own arity check refuses a missing value before this parser runs. The missing-token branch
    /// guards an option that sets <c>ArgumentArity.ZeroOrOne</c>, which would otherwise index an
    /// empty token list.
    /// <para>
    /// The value returned after an error is read by nobody: <c>Program.RunAsync</c> returns on a
    /// non-empty <c>ParseResult.Errors</c> before invoking the command. It is not a sentinel —
    /// <c>default(T)</c> is a real choice for every set here, <see cref="FailOn.None"/> among them.
    /// </para>
    /// </remarks>
    public T Parse(ArgumentResult result, string optionName)
    {
        if (result.Tokens.Count == 0)
        {
            result.AddError($"{optionName} needs a value. Valid values: {HelpList}.");
            return default;
        }

        string token = result.Tokens[0].Value;

        foreach ((string spelling, T value) in choices)
        {
            if (string.Equals(spelling, token, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        result.AddError($"'{token}' is not a valid value for {optionName}. Valid values: {HelpList}.");
        return default;
    }
}
