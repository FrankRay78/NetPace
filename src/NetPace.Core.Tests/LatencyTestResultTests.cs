using System.Reflection;
using System.Runtime.CompilerServices;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Verifies every value <see cref="LatencyTestResult"/> reports must be stated at construction,
/// so a consumer's compiler rejects a partially-populated result rather than defaulting it.
/// </summary>
public sealed class LatencyTestResultTests
{
    [Fact]
    public void LatencyTestResult_EveryReportedValue_MustBeStatedAtConstruction()
    {
        // Given
        var properties = typeof(LatencyTestResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // When
        var optional = properties
            .Where(property => property.GetCustomAttribute<RequiredMemberAttribute>() is null)
            .Select(property => property.Name)
            .ToArray();

        // Then
        properties.ShouldNotBeEmpty();
        optional.ShouldBeEmpty(
            $"LatencyTestResult properties can be omitted at construction: {string.Join(", ", optional)}. "
            + "A result that silently defaults a value it reports looks real but is not.");
    }
}
