using System.Reflection;
using System.Runtime.CompilerServices;
using Shouldly;

namespace NetPace.Core.Tests;

public partial class SpeedTestResultTests
{
    /// <summary>
    /// Verifies every value <see cref="SpeedTestResult"/> reports must be stated at construction,
    /// so a consumer's compiler rejects a partially-populated result rather than defaulting it.
    /// </summary>
    public sealed class RequiredProperties
    {
        [Fact]
        public void SpeedTestResult_EveryReportedValue_MustBeStatedAtConstruction()
        {
            // Given
            var properties = typeof(SpeedTestResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // When
            var optional = properties
                .Where(property => property.GetCustomAttribute<RequiredMemberAttribute>() is null)
                .Select(property => property.Name)
                .ToArray();

            // Then
            properties.ShouldNotBeEmpty();
            optional.ShouldBeEmpty(
                $"SpeedTestResult properties can be omitted at construction: {string.Join(", ", optional)}. "
                + "A result that silently defaults a value it reports looks real but is not.");
        }
    }
}
