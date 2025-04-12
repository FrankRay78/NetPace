using System.ComponentModel;
using System.Globalization;

namespace NetPace.Console;

[Flags]
public enum Verbosity
{
    Minimal = 1,
    Normal = 2,
    Debug = 4
}
