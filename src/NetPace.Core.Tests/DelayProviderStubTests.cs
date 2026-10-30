using System.Diagnostics;
using NetPace.Core.Clients.Testing;

namespace NetPace.Core.Tests;

public sealed class DelayProviderStubTests
{
    [Fact]
    public async Task DelayAsync_EachRequest_IsRecordedInTheOrderItWasMade()
    {
        // Given
        var stub = new DelayProviderStub();

        // When
        await stub.DelayAsync(500);
        await stub.DelayAsync(100);
        await stub.DelayAsync(500);

        // Then a consumer can assert which pauses its own code asked for.
        Assert.Equal([500, 100, 500], stub.RequestedDelays);
        Assert.Equal(3, stub.DelayCallCount);
    }

    [Fact]
    public void ANewDelayProviderStub_BeforeAnyRequest_HasRecordedNothing()
    {
        // Given, When
        var stub = new DelayProviderStub();

        // Then
        Assert.Empty(stub.RequestedDelays);
        Assert.Equal(0, stub.DelayCallCount);
    }

    [Fact]
    public async Task DelayAsync_ALongDelay_CompletesWithoutWaiting()
    {
        // Given a pause long enough that waiting for it would be obvious.
        var stub = new DelayProviderStub();

        // When
        var stopwatch = Stopwatch.StartNew();
        await stub.DelayAsync(60_000);
        stopwatch.Stop();

        // Then a consumer's test of delayed code runs at once rather than in real time.
        Assert.True(stopwatch.ElapsedMilliseconds < 1_000, $"expected no real wait, took {stopwatch.ElapsedMilliseconds}ms");
        Assert.Equal([60_000], stub.RequestedDelays);
    }

    [Fact]
    public async Task DelayAsync_WithACancelledToken_ThrowsAndRecordsNothing()
    {
        // Given
        var stub = new DelayProviderStub();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // When, Then a consumer testing a cancelled run sees the pause abandoned.
        await Assert.ThrowsAsync<OperationCanceledException>(() => stub.DelayAsync(100, source.Token));
        Assert.Empty(stub.RequestedDelays);
    }
}
