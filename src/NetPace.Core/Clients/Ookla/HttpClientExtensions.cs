public static class HttpClientExtensions
{
    internal static async Task<string> GetStringWithTimeoutAsync(
        this HttpClient client,
        string requestUri,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        return await client.GetStringAsync(requestUri, linkedCts.Token).ConfigureAwait(false);
    }
}
