namespace NetPace.Core.Clients.Ookla.Extensions;

internal static class HttpClientExtensions
{
    internal static async Task<string> GetStringWithTimeoutAsync(
        this HttpClient client,
        string requestUri,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await client.GetStringAsync(requestUri, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // A request abandoned at this timeout is a network outcome, not the caller asking to
            // stop, and both arrive as the same exception type. It is re-raised in the shape
            // HttpClient uses for its own Timeout - a cancellation carrying a TimeoutException -
            // so a caller can tell the two apart without knowing how the timeout was applied.
            throw new TaskCanceledException(
                $"The request was canceled due to the configured timeout of {timeout.TotalSeconds} seconds elapsing.",
                new TimeoutException(e.Message, e),
                timeoutCts.Token);
        }
    }
}
