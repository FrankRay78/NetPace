using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace NetPace.Core;

// Streams cryptographically-random bytes on demand in small chunks, so a large upload body never
// allocates a single large byte[] on the large object heap.
internal sealed class RandomStreamContent : HttpContent
{
    private readonly long totalSize;
    private readonly int chunkSize;
    private readonly Action<int>? onBytesWritten;

    // onBytesWritten is called with each chunk's length once that chunk has been written, so a
    // caller can count what an upload sent even when the server closes the connection partway.
    public RandomStreamContent(long totalSize, Action<int>? onBytesWritten = null, int chunkSize = 8192)
    {
        this.totalSize = totalSize;
        this.onBytesWritten = onBytesWritten;
        this.chunkSize = chunkSize > 0 ? chunkSize : 8192;
        Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    }

    // A write returns once the transport has accepted the data, so this counts bytes handed over
    // rather than bytes known to have arrived.
    public long BytesWritten { get; private set; }

    protected override bool TryComputeLength(out long length)
    {
        length = totalSize;
        return true;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    // The cancellable overload is the one implemented, so a cancelled upload stops writing with its
    // request: a chunk reported after the request's accounting has been read would count towards
    // the transfer total without appearing on that request's diagnostic record.
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        try
        {
            long remaining = totalSize;
            while (remaining > 0)
            {
                var toWrite = (int)Math.Min(buffer.Length, remaining);
                RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
                await stream.WriteAsync(buffer.AsMemory(0, toWrite), cancellationToken).ConfigureAwait(false);
                remaining -= toWrite;

                // After the write, so a chunk whose write throws is not reported as sent. One the
                // transport took and never delivered still is, which GetUploadSpeedAsync records.
                BytesWritten += toWrite;
                onBytesWritten?.Invoke(toWrite);
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
