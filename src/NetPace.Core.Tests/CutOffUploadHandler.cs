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

    public CutOffUploadHandler(int bytesBeforeCutOff) => this.bytesBeforeCutOff = bytesBeforeCutOff;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var sink = new CutOffRequestStream(bytesBeforeCutOff);

            try
            {
                await request.Content.CopyToAsync(sink, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // What a client is handed when a connection goes while it is still writing.
                // Cancellation is let through as itself: the provider tells a cancelled request
                // from a failed one by catching OperationCanceledException, so relabelling it here
                // would have an upload stopped by the byte budget counted as a failure.
                throw new HttpRequestException("Error while copying content to a stream.", e);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
    }
}
