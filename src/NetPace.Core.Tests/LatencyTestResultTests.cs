using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Verifies <see cref="LatencyTestResult"/> carries the metadata that makes a consumer's compiler
/// reject a partially-populated result: every settable property is required, and no constructor
/// opts back out of it.
/// </summary>
public sealed class LatencyTestResultTests
{
    [Fact]
    public void LatencyTestResult_EveryReportedValue_MustBeStatedAtConstruction()
    {
        // Mechanism-pinned under Constitution §IX's regression exception: PR #222 shipped
        // VariableSpeedTester reporting 0.25 Mbps against zero requests attempted, from a
        // zero-defaulted placeholder result. `required` has no runtime enforcement, so the
        // metadata the compiler emits is the only instrument available in-suite.

        // Given
        var settable = typeof(LatencyTestResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .ToArray();

        // When
        var optional = settable
            .Where(property => property.GetCustomAttribute<RequiredMemberAttribute>() is null)
            .Select(property => property.Name)
            .ToArray();

        var opsOut = typeof(LatencyTestResult)
            .GetConstructors()
            .Where(constructor => constructor.GetCustomAttribute<SetsRequiredMembersAttribute>() is not null)
            .ToArray();

        // Then
        settable.ShouldNotBeEmpty();
        optional.ShouldBeEmpty(
            $"LatencyTestResult properties can be omitted at construction: {string.Join(", ", optional)}. "
            + "A result that silently defaults a value it reports looks real but is not.");
        opsOut.ShouldBeEmpty(
            "A [SetsRequiredMembers] constructor restores silent defaulting while leaving every "
            + "RequiredMemberAttribute in place, so the check above cannot see it.");
    }
}
