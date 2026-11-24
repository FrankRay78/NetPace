using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class SpeedTestResultTests
{
    public sealed class RequiredProperties
    {
        /// <summary>
        /// Verifies <see cref="SpeedTestResult"/> carries the metadata that makes a consumer's compiler
        /// reject a partially-populated result: every settable property is required, and no constructor
        /// opts back out of it.
        /// </summary>
        [Fact]
        public void SpeedTestResult_EveryReportedValue_MustBeStatedAtConstruction()
        {
            // Regression, PR #222: a zero-defaulted placeholder result reported a speed against
            // zero requests. `required` has no runtime enforcement, so metadata is the instrument.

            // Given
            var settable = typeof(SpeedTestResult)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is not null)
                .ToArray();

            // When
            var optional = settable
                .Where(property => property.GetCustomAttribute<RequiredMemberAttribute>() is null)
                .Select(property => property.Name)
                .ToArray();

            var opsOut = typeof(SpeedTestResult)
                .GetConstructors()
                .Where(constructor => constructor.GetCustomAttribute<SetsRequiredMembersAttribute>() is not null)
                .ToArray();

            // Then
            settable.ShouldNotBeEmpty();
            optional.ShouldBeEmpty(
                $"SpeedTestResult properties can be omitted at construction: {string.Join(", ", optional)}. "
                + "A result that silently defaults a value it reports looks real but is not.");
            opsOut.ShouldBeEmpty(
                "A [SetsRequiredMembers] constructor restores silent defaulting while leaving every "
                + "RequiredMemberAttribute in place, so the check above cannot see it.");
        }
    }
}
