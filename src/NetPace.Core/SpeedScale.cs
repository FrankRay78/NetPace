namespace NetPace.Core;

/// <summary>
/// Represents the scale of speed unit measurement.
/// </summary>
public enum SpeedScale
{
    /// <summary>
    /// Automatically determine the most appropriate scale.
    /// </summary>
    Auto,

    /// <summary>
    /// Base units (bps/Bps).
    /// </summary>
    Base,

    /// <summary>
    /// Kilo scale (Kbps/KBps or Kibps/KiBps).
    /// </summary>
    Kilo,

    /// <summary>
    /// Mega scale (Mbps/MBps or Mibps/MiBps).
    /// </summary>
    Mega,

    /// <summary>
    /// Giga scale (Gbps/GBps or Gibps/GiBps).
    /// </summary>
    Giga,

    /// <summary>
    /// Tera scale (Tbps/TBps or Tibps/TiBps).
    /// </summary>
    Tera,

    /// <summary>
    /// Peta scale (Pbps/PBps or Pibps/PiBps).
    /// </summary>
    Peta
}
