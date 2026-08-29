namespace NetPace.Console;

/// <summary>
/// Specifies how file output should behave when writing to files.
/// </summary>
public enum FileMode
{
    /// <summary>
    /// Append output to existing files or create new ones if they don't exist.
    /// </summary>
    Append,

    /// <summary>
    /// Overwrite existing files or create new ones if they don't exist.
    /// </summary>
    Overwrite
}
