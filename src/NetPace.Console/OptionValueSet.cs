using System.CommandLine.Parsing;

namespace NetPace.Console;

/// <summary>
/// The spellings a user may type for one option with a fixed set of choices, and the value each
/// spelling binds to. One instance answers all three questions about that option's values — how
/// the help lists them, which value a typed token means, and what to say when the token is not one
/// of them — so the three cannot drift apart.
/// </summary>
/// <typeparam name="T">The enumeration the option binds to.</typeparam>
internal sealed class OptionValueSet<T> where T : struct, Enum
{
    private readonly (string Spelling, T Value)[] _choices;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionValueSet{T}"/> class.
    /// </summary>
    /// <param name="choices">
    /// The choices, in the order the help lists them — which need not be the order the enumeration
    /// declares its members in.
    /// </param>
    public OptionValueSet(params (string Spelling, T Value)[] choices)
    {
        _choices = choices;
    }

    /// <summary>
    /// The choices as the help lists them, for example <c>&lt;bits, bytes&gt;</c>.
    /// </summary>
    public string HelpList => $"<{string.Join(", ", _choices.Select(choice => choice.Spelling))}>";

    /// <summary>
    /// The spelling the help uses for <paramref name="value"/>.
    /// </summary>
    public string Spell(T value)
    {
        foreach ((string spelling, T candidate) in _choices)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, value))
            {
                return spelling;
            }
        }

        return value.ToString();
    }

    /// <summary>
    /// Bind the token the user typed, in any case. A token that is not one of the choices is
    /// reported against <paramref name="optionName"/>, listing the valid values exactly as the
    /// help lists them.
    /// </summary>
    public T Parse(ArgumentResult result, string optionName)
    {
        string token = result.Tokens[0].Value;

        foreach ((string spelling, T value) in _choices)
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
