using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace NetPace.Core;

/// <summary>
/// HttpContent that streams cryptographically-random bytes on demand in small chunks.
/// Avoids allocating a single large byte[] and prevents LOH allocations.
/// </summary>
internal sealed class RandomStreamContent : HttpContent
{
    private readonly long totalSize;
    private readonly int chunkSize;

    public RandomStreamContent(long totalSize, int chunkSize = 8192)
    {
        this.totalSize = totalSize;
        this.chunkSize = chunkSize > 0 ? chunkSize : 8192;
        Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = totalSize;
        return true;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        try
        {
            long remaining = totalSize;
            while (remaining > 0)
            {
                var toWrite = (int)Math.Min(buffer.Length, remaining);
                RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
                await stream.WriteAsync(buffer.AsMemory(0, toWrite)).ConfigureAwait(false);
                remaining -= toWrite;
            }

            await stream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
