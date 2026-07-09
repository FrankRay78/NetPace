using System.Runtime.CompilerServices;
using DiffEngine;

namespace NetPace.Console.Tests;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Init()
    {
        Verifier.UseProjectRelativeDirectory("Expectations");

        // Suppress auto-launching the diff viewer when a Verify snapshot test fails.
        // Without this, vim/code launches per-failure in CI/headless runs and can leave
        // .swp files behind; we want clean failures instead.
        DiffRunner.Disabled = true;
    }
}
