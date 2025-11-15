using System.ComponentModel;
using System.Globalization;

namespace NetPace.Console;

/// <summary>
/// Specifies the verbosity level for console output.
/// </summary>
[Flags]
public enum Verbosity
{
    /// <summary>
    /// Minimal output, ideal for batch scripts and redirected output.
    /// </summary>
    Minimal = 1,

    /// <summary>
    /// Normal output with standard information for interactive users.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Verbose output with detailed debugging information.
    /// </summary>
    Debug = 4
}
