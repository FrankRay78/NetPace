using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetPace.Core.Tests;

/// <summary>
/// A response body that hands over a fixed number of bytes and is then cut off, the way a server
/// behaves when it closes a connection it has decided is too old to keep writing to.
/// </summary>
/// <remarks>
/// A test uses this rather than a short body because the two are not the same thing: a short body
/// is a complete response, while this one faults the read that would have continued it, so the
/// request fails after data has already arrived.
/// </remarks>
internal sealed class CutOffResponseStream : Stream
{
    private readonly int bytesBeforeCutOff;
    private readonly TaskCompletionSource? cutOffReached;
    private readonly bool waitForCancellationInsteadOfFaulting;

    private int bytesDelivered;

    /// <summary>
    /// Constructs a response body that is cut off after the given number of bytes.
    /// </summary>
    /// <param name="bytesBeforeCutOff">How many bytes the stream hands over before it stops.</param>
    /// <param name="cutOffReached">Signalled once those bytes have been handed over, so a test can order what happens next.</param>
    /// <param name="waitForCancellationInsteadOfFaulting">
    /// When set, the stream blocks until its reader is cancelled instead of faulting, which is how
    /// a transfer still in flight behaves when the test it belongs to ends around it.
    /// </param>
    public CutOffResponseStream(int bytesBeforeCutOff, TaskCompletionSource? cutOffReached = null, bool waitForCancellationInsteadOfFaulting = false)
    {
        this.bytesBeforeCutOff = bytesBeforeCutOff;
        this.cutOffReached = cutOffReached;
        this.waitForCancellationInsteadOfFaulting = waitForCancellationInsteadOfFaulting;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var remaining = bytesBeforeCutOff - bytesDelivered;

        if (remaining > 0)
        {
            var count = Math.Min(remaining, buffer.Length);
            buffer.Span[..count].Clear();
            bytesDelivered += count;

            if (bytesDelivered == bytesBeforeCutOff)
            {
                cutOffReached?.TrySetResult();
            }

            return count;
        }

        if (waitForCancellationInsteadOfFaulting)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        throw new IOException("The response ended prematurely.");
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

