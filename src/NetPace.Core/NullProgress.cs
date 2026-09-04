
/// <summary>
/// A no-op implementation of IProgress{T} for internal use.
/// </summary>
public sealed class NullProgress<T> : IProgress<T>
{
    /// <inheritdoc/>
    public void Report(T value) { }
}
