using System.Diagnostics.CodeAnalysis;

namespace NetPace.Console;

/// <summary>
/// A user-defined key and value recorded on every CSV row and in every JSON result of a run, so a
/// log can carry context NetPace knows nothing about — an ISP plan, a site name, a connection type.
/// </summary>
/// <remarks>
/// NetPace does not police the keys. A key matching a built-in field name, or repeating an earlier
/// key, is recorded as given, and each output format resolves that in its own shape: CSV writes a
/// second column of that name, while JSON groups every pair in one object and keeps the last value
/// given for a repeated key.
/// </remarks>
public sealed record CustomField
{
    /// <summary>
    /// The form a custom field argument must take, quoted back to the user when one does not.
    /// </summary>
    internal const string ExpectedForm = "key=value";

    /// <summary>
    /// Separates a custom field's key from its value on the command line.
    /// </summary>
    private const char Separator = '=';

    /// <summary>
    /// Gets the key, which names the CSV column and the JSON property the value is recorded under.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the value, recorded exactly as given.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Reads one <c>key=value</c> argument. The key is everything before the first separator and
    /// the value everything after it, so a value may carry a separator of its own and may be empty.
    /// The key must be a single line, because CSV writes a line break as a space: a key carrying one
    /// would name a different column than the JSON property of the same pair, and the two formats
    /// are meant to carry the same fields under the same names.
    /// </summary>
    /// <param name="argument">The argument as it arrived on the command line.</param>
    /// <param name="field">The field read, or <see langword="null"/> when the argument is not one.</param>
    /// <returns>
    /// <see langword="true"/> when the argument holds a separator with a non-empty single-line key
    /// before it; <see langword="false"/> when it holds no separator, nothing before the first one,
    /// or a key spanning more than one line.
    /// </returns>
    public static bool TryParse(string argument, [NotNullWhen(true)] out CustomField? field)
    {
        field = null;

        int separator = argument.IndexOf(Separator);
        if (separator <= 0)
        {
            return false;
        }

        string key = argument[..separator];
        if (key.Contains('\r') || key.Contains('\n'))
        {
            return false;
        }

        field = new CustomField
        {
            Key = key,
            Value = argument[(separator + 1)..]
        };

        return true;
    }

    /// <summary>
    /// Reads every <c>key=value</c> argument, keeping the order they were given in.
    /// </summary>
    /// <remarks>
    /// Nothing is dropped. The <c>--custom-field</c> option's own validator fails the parse first,
    /// so a malformed argument from the command line stops the run with a message naming the
    /// expected form before any test is performed; one reaching here has bypassed that validation,
    /// and is reported rather than silently left out.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// An argument is not in the form <c>key=value</c>.
    /// </exception>
    public static IReadOnlyList<CustomField> ParseAll(IEnumerable<string>? arguments)
    {
        if (arguments is null)
        {
            return [];
        }

        var fields = new List<CustomField>();
        foreach (string argument in arguments)
        {
            if (!TryParse(argument, out CustomField? field))
            {
                throw new ArgumentException($"'{argument}' is not in the form {ExpectedForm}.", nameof(arguments));
            }

            fields.Add(field);
        }

        return fields;
    }
}
