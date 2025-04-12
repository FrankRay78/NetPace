using System.Runtime.CompilerServices;

namespace NetPace.Tests;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Init()
    {
        Verifier.UseProjectRelativeDirectory("Expectations");
    }
}
