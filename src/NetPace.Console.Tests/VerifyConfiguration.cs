using System.Runtime.CompilerServices;
using DiffEngine;

namespace NetPace.Console.Tests;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Init()
    {
        Verifier.UseProjectRelativeDirectory("Expectations");

        // Without this, a failing snapshot launches a diff viewer per failure in a headless run
        // and can leave .swp files behind.
        DiffRunner.Disabled = true;
    }
}
