using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NetPace.Core.Tests;

/// <summary>
/// A server that accepts a fixed number of bytes of an upload and then closes the connection,
/// which the client sees as a transport failure partway through sending the body.
/// </summary>
/// <remarks>
/// A handler of its own rather than a mocked response, because the point of the test it serves is
/// that the body was really being serialised when the connection went: a mock that never reads the
/// request content has nothing to cut off.
/// </remarks>
internal sealed class CutOffUploadHandler : HttpMessageHandler
{
    private readonly int bytesBeforeCutOff;
    private readonly bool answerSuccessAfterCutOff;

    /// <summary>
    /// Constructs a server that accepts <paramref name="bytesBeforeCutOff"/> bytes of an upload.
    /// </summary>
    /// <param name="bytesBeforeCutOff">How much of the body is read before reading stops.</param>
    /// <param name="answerSuccessAfterCutOff">
    /// When set, the server stops reading and answers success instead of closing the connection,
    /// so the upload succeeds with only part of its body sent.
    /// </param>
    public CutOffUploadHandler(int bytesBeforeCutOff, bool answerSuccessAfterCutOff = false)
    {
        this.bytesBeforeCutOff = bytesBeforeCutOff;
        this.answerSuccessAfterCutOff = answerSuccessAfterCutOff;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var sink = new CutOffRequestStream(bytesBeforeCutOff);

            try
            {
                await request.Content.CopyToAsync(sink, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (answerSuccessAfterCutOff && e is not OperationCanceledException)
            {
                // Stopped reading partway, and answers success regardless. The cut-off reaches
                // here wrapped by HttpContent, so it is caught whatever its type.
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Cancellation is let through as itself: the provider tells a cancelled request
                // from a failed one by its type, so relabelling would miscount a budget stop.
                throw new HttpRequestException("Error while copying content to a stream.", e);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
    }
}
