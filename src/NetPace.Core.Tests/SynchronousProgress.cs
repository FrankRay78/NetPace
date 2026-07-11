using System;

namespace NetPace.Core.Tests;

/// <summary>
/// A synchronous <see cref="IProgress{T}"/> for tests: invokes the handler inline on the
/// thread that calls <see cref="Report"/>, so collected values reflect the production emit
/// order deterministically.
/// </summary>
/// <remarks>
/// <see cref="System.Progress{T}"/> is unsuitable for asserting on a progress sequence: it posts
/// each handler invocation to the captured synchronization context (the thread pool in a unit-test
/// run), so callbacks can arrive out of order — or even after the awaited operation has returned
/// and the assertion has already run. That non-determinism was the source of the historic
/// progress-test flakiness. The Ookla progress paths emit reports in a well-defined order
/// (sequential loops, or serialised under a lock), so observing them synchronously is exact.
/// </remarks>
internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> handler;

    public SynchronousProgress(Action<T> handler) => this.handler = handler;

    public void Report(T value) => handler(value);
}
