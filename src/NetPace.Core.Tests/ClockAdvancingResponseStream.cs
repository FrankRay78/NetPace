using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetPace.Core.Tests;

/// <summary>
/// A response body that hands over a fixed number of equal chunks, moving a test's clock on by a
/// stated amount before each one, so a test can place chunks inside and outside the interval that
/// paces the live progress figure.
/// </summary>
/// <remarks>
/// The clock moves inside the read, because the pacing is decided where the bytes are counted:
/// advancing from the test thread would race it.
/// </remarks>
internal sealed class ClockAdvancingResponseStream : Stream
{
    private readonly int chunkSize;
    private readonly ControlledTimeProvider clock;
    private readonly TimeSpan[] advanceBeforeEachChunk;

    private int chunksDelivered;

    /// <summary>
    /// Constructs a body of <paramref name="advanceBeforeEachChunk"/>.Length chunks of
    /// <paramref name="chunkSize"/> bytes, moving <paramref name="clock"/> on by that entry's
    /// amount before each chunk is handed over.
    /// </summary>
    public ClockAdvancingResponseStream(int chunkSize, ControlledTimeProvider clock, params TimeSpan[] advanceBeforeEachChunk)
    {
        this.chunkSize = chunkSize;
        this.clock = clock;
        this.advanceBeforeEachChunk = advanceBeforeEachChunk;
    }

    /// <summary>How many bytes the whole body carries.</summary>
    public int TotalBytes => chunkSize * advanceBeforeEachChunk.Length;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (chunksDelivered == advanceBeforeEachChunk.Length)
        {
            return ValueTask.FromResult(0);
        }

        clock.Advance(advanceBeforeEachChunk[chunksDelivered]);
        chunksDelivered++;

        var count = Math.Min(chunkSize, buffer.Length);
        buffer.Span[..count].Clear();
        return ValueTask.FromResult(count);
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
