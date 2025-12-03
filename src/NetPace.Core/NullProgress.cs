
/// <summary>
/// A no-op implementation of IProgress{T} for internal use.
/// </summary>
internal sealed class NullProgress<T> : IProgress<T>
{
    public void Report(T value) { }
}