namespace NetPace.Core;

/// <summary>
/// Represents the unit of measurement used for expressing network speed.
/// </summary>
public enum SpeedUnit
{
    /// <summary>
    /// Speed measured in bits per second (bps).
    /// Commonly used in networking contexts.
    /// </summary>
    BitsPerSecond,

    /// <summary>
    /// Speed measured in bytes per second (Bps).
    /// Useful when presenting data rates in terms of file transfer or storage.
    /// </summary>
    BytesPerSecond,
}