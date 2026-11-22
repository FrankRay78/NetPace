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

    /// <summary>Finished with no request succeeding: every request failed, or the measurement threw. A transfer in this state can still show a figure, for what its requests moved before they failed.</summary>
    Failed,
}
