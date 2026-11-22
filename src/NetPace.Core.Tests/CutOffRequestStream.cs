using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetPace.Core.Tests;

/// <summary>
/// A request body sink that accepts a fixed number of bytes and is then cut off, the way a server
/// behaves when it closes a connection while the client is still writing to it.
/// </summary>
internal sealed class CutOffRequestStream : Stream
{
    private readonly int bytesBeforeCutOff;

    /// <summary>How many bytes the sink has accepted so far.</summary>
    private int bytesAccepted;

    public CutOffRequestStream(int bytesBeforeCutOff) => this.bytesBeforeCutOff = bytesBeforeCutOff;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (bytesAccepted + buffer.Length > bytesBeforeCutOff)
        {
            throw new IOException("The server closed the connection.");
        }

        bytesAccepted += buffer.Length;
        return ValueTask.CompletedTask;
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
