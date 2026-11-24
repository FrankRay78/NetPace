using System;

namespace NetPace.Core.Tests;

/// <summary>
/// A synchronous <see cref="IProgress{T}"/> for tests: invokes the handler inline on the
/// thread that calls <see cref="Report"/>, so collected values reflect the production emit
/// order deterministically.
/// </summary>
/// <remarks>
/// <see cref="System.Progress{T}"/> posts each invocation to the captured synchronization context,
/// so callbacks can arrive out of order or after the awaited operation has returned.
/// <para>
/// The provider's own paths emit reports in a defined order, so observing them synchronously is
/// exact.
/// </para>
/// </remarks>
internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> handler;

    public SynchronousProgress(Action<T> handler) => this.handler = handler;

    public void Report(T value) => handler(value);
}
