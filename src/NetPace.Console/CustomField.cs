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
    /// </summary>
    /// <param name="argument">The argument as it arrived on the command line.</param>
    /// <param name="field">The field read, or <see langword="null"/> when the argument is not one.</param>
    /// <returns>
    /// <see langword="true"/> when the argument carries a key and a value; <see langword="false"/>
    /// when it holds no separator, or nothing before the first one.
    /// </returns>
    public static bool TryParse(string? argument, out CustomField? field)
    {
        field = null;

        if (argument is null)
        {
            return false;
        }

        int separator = argument.IndexOf(Separator);
        if (separator <= 0)
        {
            return false;
        }

        field = new CustomField
        {
            Key = argument[..separator],
            Value = argument[(separator + 1)..]
        };

        return true;
    }

    /// <summary>
    /// Reads every <c>key=value</c> argument, keeping the order they were given in.
    /// </summary>
    /// <remarks>
    /// An argument that is not a key and a value is left out, and none ever reaches here: the
    /// <c>--custom-field</c> option's own validator fails the parse first, so the run stops with a
    /// message naming the expected form before any test is performed.
    /// </remarks>
    public static IReadOnlyList<CustomField> ParseAll(IEnumerable<string>? arguments)
    {
        if (arguments is null)
        {
            return [];
        }

        var fields = new List<CustomField>();
        foreach (string argument in arguments)
        {
            if (TryParse(argument, out CustomField? field))
            {
                fields.Add(field!);
            }
        }

        return fields;
    }
}
