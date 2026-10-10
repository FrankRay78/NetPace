namespace NetPace.Console.Diagnostics;

/// <summary>
/// The recorder in play when <c>--diagnostics</c> was not passed: records nothing and writes
/// nothing, so no call site needs to ask whether diagnostics are switched on.
/// </summary>
public sealed class NullDiagnosticRecorder : IDiagnosticRecorder
{
    /// <summary>
    /// The single instance; it holds no state.
    /// </summary>
    public static readonly NullDiagnosticRecorder Instance = new();

    private NullDiagnosticRecorder() { }

    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public void Record(string eventName, params (string Key, string? Value)[] fields) { }

    /// <inheritdoc />
    public void RecordAt(DateTime timestamp, string eventName, params (string Key, string? Value)[] fields) { }

    /// <inheritdoc />
    public void Flush() { }
}
