namespace NetPace.Console.Tests;

public sealed class OptionValuesTests
{
    [Fact]
    public void Spell_EveryValueOfEveryOption_MatchesTheSpellingItsChoiceListShows()
    {
        // Given — the help renders a choice list from each set, but reads the DEFAULT column back
        // off a boxed value through OptionValues.Spell. The two must agree, or an option's default
        // prints in a spelling its own choice list does not offer.
        AssertDefaultColumnAgrees(OptionValues.Profiles);
        AssertDefaultColumnAgrees(OptionValues.Units);
        AssertDefaultColumnAgrees(OptionValues.UnitScales);
        AssertDefaultColumnAgrees(OptionValues.UnitSystems);
        AssertDefaultColumnAgrees(OptionValues.FileModes);
        AssertDefaultColumnAgrees(OptionValues.FailOnModes);

        static void AssertDefaultColumnAgrees<T>(OptionValueSet<T> set) where T : struct, Enum
        {
            foreach (T value in Enum.GetValues<T>())
            {
                Assert.Equal(set.Spell(value), OptionValues.Spell(value));
            }
        }
    }
}
