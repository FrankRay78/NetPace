using System.Runtime.CompilerServices;
using DiffEngine;

namespace NetPace.Console.Tests;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Init()
    {
        Verifier.UseProjectRelativeDirectory("Expectations");
    }
}
