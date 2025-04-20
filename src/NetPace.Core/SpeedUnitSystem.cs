namespace NetPace.Core;

/// <summary>
/// Represents the unit system used to express network speed measurements.
/// </summary>
public enum SpeedUnitSystem
{
    /// <summary>
    /// SI (International System of Units) based on powers of 1000 (e.g., Kbps, Mbps, Gbps).
    /// Commonly used in networking and telecommunications.
    /// </summary>
    SI,

    /// <summary>
    /// IEC (International Electrotechnical Commission) units based on powers of 1024 (e.g., Kibps, Mibps, Gibps).
    /// More precise in computing contexts where binary multiples are standard.
    /// </summary>
    IEC
}