using System.Runtime.CompilerServices;

namespace NetPace.Console.Tests;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Init()
    {
        Verifier.UseProjectRelativeDirectory("Expectations");
    }
}
