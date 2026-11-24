using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NetPace.Core.Tests;

/// <summary>
/// A server receiving two uploads at once: one it accepts a little of and then stops reading, and
/// one of a given length that it accepts in full, but only once the first has stalled.
/// </summary>
/// <remarks>
/// <para>
/// Orders a byte budget being reached while another upload is in flight: the filling upload is
/// held back, so the stalled one is always partway through its body when the budget ends it.
/// </para>
/// <para>
/// The stall is bounded, because a client that did not pass its cancellation through to the write
/// would otherwise hang the test.
/// </para>
/// </remarks>
internal sealed class StalledUploadHandler : HttpMessageHandler
{
    private static readonly TimeSpan StallLimit = TimeSpan.FromSeconds(5);

    private readonly long acceptedInFullLength;
    private readonly int bytesBeforeStall;
    private readonly TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long bytesAcceptedBeforeStall;

    /// <summary>
    /// How much of the stalled upload the server had accepted when it stopped reading: whole
    /// writes, so at least the threshold given and as much more as the last write carried.
    /// </summary>
    public long BytesAcceptedBeforeStall => Interlocked.Read(ref bytesAcceptedBeforeStall);

    /// <summary>
    /// Constructs a server that stalls every upload but the one of <paramref name="acceptedInFullLength"/> bytes.
    /// </summary>
    /// <param name="acceptedInFullLength">The length of the upload accepted in full.</param>
    /// <param name="bytesBeforeStall">How much of any other upload is accepted before reading stops.</param>
    public StalledUploadHandler(long acceptedInFullLength, int bytesBeforeStall)
    {
        this.acceptedInFullLength = acceptedInFullLength;
        this.bytesBeforeStall = bytesBeforeStall;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var length = request.Content?.Headers.ContentLength ?? 0;

        // The empty probe that resolves the upload address, answered at once.
        if (request.Content is not null && length > 0)
        {
            if (length == acceptedInFullLength)
            {
                await stalled.Task.WaitAsync(StallLimit, cancellationToken).ConfigureAwait(false);
                await request.Content.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var sink = new StallingSink(bytesBeforeStall, stalled, accepted => Interlocked.Exchange(ref bytesAcceptedBeforeStall, accepted));
                await request.Content.CopyToAsync(sink, cancellationToken).ConfigureAwait(false);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
    }

    /// <summary>
    /// Accepts a fixed number of bytes, then holds the next write until the writer's cancellation
    /// token fires, or until <see cref="StallLimit"/> passes and it accepts that write and the rest.
    /// </summary>
    private sealed class StallingSink(int bytesBeforeStall, TaskCompletionSource stalled, Action<long> onStall) : Stream
    {
        private int bytesAccepted;
        private bool stallRanOut;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (bytesAccepted >= bytesBeforeStall && !stallRanOut)
            {
                onStall(bytesAccepted);
                stalled.TrySetResult();
                await Task.Delay(StallLimit, cancellationToken).ConfigureAwait(false);
                stallRanOut = true;
            }

            bytesAccepted += buffer.Length;
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
