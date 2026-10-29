using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// Every value a user can type for an option with a fixed set of choices, in one place. What the
/// help shows is typeable verbatim, and a typed token matches in any case. The spellings are
/// lowercase apart from the <c>SI</c> and <c>IEC</c> abbreviations, which are shown uppercase.
/// </summary>
/// <remarks>
/// The spellings are a CLI concern, not a library one: <see cref="SpeedUnit"/>,
/// <see cref="SpeedScale"/>, <see cref="SpeedUnitSystem"/> and <see cref="Profile"/> are published
/// <c>NetPace.Core</c> members and keep their descriptive names for library consumers, who never
/// see a command line. <see cref="FileMode"/> and <see cref="FailOn"/> are CLI-only and are listed
/// here for the same reason as the rest — one place per option.
/// </remarks>
internal static class OptionValues
{
    public static readonly OptionValueSet<Profile> Profiles = new(
        ("tiny", Profile.Tiny),
        ("small", Profile.Small),
        ("medium", Profile.Medium),
        ("large", Profile.Large),
        ("mega", Profile.Mega));

    public static readonly OptionValueSet<SpeedUnit> Units = new(
        ("bits", SpeedUnit.BitsPerSecond),
        ("bytes", SpeedUnit.BytesPerSecond));

    public static readonly OptionValueSet<SpeedScale> UnitScales = new(
        ("auto", SpeedScale.Auto),
        ("base", SpeedScale.Base),
        ("kilo", SpeedScale.Kilo),
        ("mega", SpeedScale.Mega),
        ("giga", SpeedScale.Giga),
        ("tera", SpeedScale.Tera),
        ("peta", SpeedScale.Peta));

    public static readonly OptionValueSet<SpeedUnitSystem> UnitSystems = new(
        ("SI", SpeedUnitSystem.SI),
        ("IEC", SpeedUnitSystem.IEC));

    public static readonly OptionValueSet<FileMode> FileModes = new(
        ("append", FileMode.Append),
        ("overwrite", FileMode.Overwrite));

    public static readonly OptionValueSet<FailOn> FailOnModes = new(
        ("none", FailOn.None),
        ("total", FailOn.Total),
        ("partial", FailOn.Partial));

    /// <summary>
    /// The help spelling of an option's default value. The DEFAULT column reads defaults back off
    /// the options it renders, so it has only a boxed value to go on and cannot name the type.
    /// </summary>
    public static string Spell(object value)
    {
        return value switch
        {
            Profile profile => Profiles.Spell(profile),
            SpeedUnit unit => Units.Spell(unit),
            SpeedScale scale => UnitScales.Spell(scale),
            SpeedUnitSystem unitSystem => UnitSystems.Spell(unitSystem),
            FileMode fileMode => FileModes.Spell(fileMode),
            FailOn failOn => FailOnModes.Spell(failOn),
            _ => value.ToString() ?? string.Empty
        };
    }
}
