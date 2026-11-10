namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// How far a row's measurement got.
/// </summary>
internal enum SpeedTestTableRowState
{
    /// <summary>Still measuring.</summary>
    Running,

    /// <summary>Finished with every request succeeding.</summary>
    Succeeded,

    /// <summary>Finished with some requests failing, having still measured something.</summary>
    PartiallyFailed,

    /// <summary>Finished with nothing measured: every request failed, or the measurement threw.</summary>
    Failed,
}
