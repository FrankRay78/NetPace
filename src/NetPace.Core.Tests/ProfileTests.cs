using System.Reflection;
using NetPace.Core.Clients.Ookla;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Structural tests guarding the <see cref="Profile"/> enum's
/// provider-agnostic placement at the root of NetPace.Core.
/// </summary>
public sealed class ProfileTests
{
    [Fact]
    public void Profile_IsLocatedAtTopLevelOfNetPaceCore_NotUnderClients()
    {
        // SCENARIO: Profile enum is provider-agnostic and at the root of NetPace.Core

        // Given
        var profileType = typeof(Profile);

        // When
        var ns = profileType.Namespace;

        // Then
        ns.ShouldBe("NetPace.Core");
    }

    [Fact]
    public void Profile_HasNoExtensionMethodReturningProviderType()
    {
        // SCENARIO: Profile enum is provider-agnostic and at the root of NetPace.Core

        // Given
        var assembly = typeof(Profile).Assembly;

        // When
        var offendingMethods = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m =>
            {
                var ps = m.GetParameters();
                return ps.Length > 0
                    && ps[0].ParameterType == typeof(Profile)
                    && m.ReturnType.Namespace is string ns
                    && ns.StartsWith("NetPace.Core.Clients", StringComparison.Ordinal);
            })
            .ToList();

        // Then
        offendingMethods.ShouldBeEmpty();
    }

    [Fact]
    public void Profile_NoExtensionsHelperType_ExistsInAssembly()
    {
        // SCENARIO: Profile enum is provider-agnostic and at the root of NetPace.Core

        // Given
        var assembly = typeof(Profile).Assembly;

        // When
        var oseExt = assembly.GetType("NetPace.Core.Clients.Ookla.OoklaSpeedtestSettingsExtensions");
        var opExt = assembly.GetType("NetPace.Core.Clients.Ookla.OoklaProfileExtensions");

        // Then
        oseExt.ShouldBeNull();
        opExt.ShouldBeNull();
    }

    [Fact]
    public void Profile_SourceFile_LivesAtRootOfNetPaceCore()
    {
        // SCENARIO: Profile enum is provider-agnostic and at the root of NetPace.Core

        // Given
        // Resolve the repo root by walking up from the test bin directory.
        // Path.Join is used instead of Path.Combine so a rooted segment cannot silently
        // discard earlier arguments (CodeQL cs/path-combine).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Join(dir.FullName, "src", "NetPace.sln")))
        {
            dir = dir.Parent;
        }

        // When
        dir.ShouldNotBeNull();
        var profilePath = Path.Join(dir!.FullName, "src", "NetPace.Core", "Profile.cs");

        // Then
        File.Exists(profilePath).ShouldBeTrue($"expected source file at '{profilePath}'");
    }

    [Theory]
    [InlineData(Profile.Tiny)]
    [InlineData(Profile.Small)]
    [InlineData(Profile.Medium)]
    [InlineData(Profile.Large)]
    [InlineData(Profile.Mega)]
    public void Profile_AllExpectedMembers_AreDefined(Profile p)
    {
        Enum.IsDefined(typeof(Profile), p).ShouldBeTrue();
    }

    [Fact]
    public void Profile_DefaultValue_IsMedium()
    {
        // Locks the invariant that an uninitialised `Profile` resolves to the safe broadband
        // default rather than the IoT preset. Reordering enum members must not silently regress this.
        default(Profile).ShouldBe(Profile.Medium);
        ((int)Profile.Medium).ShouldBe(0);
    }
}
