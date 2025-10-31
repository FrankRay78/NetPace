using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using BenchmarkDotNet.Attributes;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;

namespace NetPace.Core.Tests;

[MemoryDiagnoser]
public class OoklaMemoryBench
{
    private OoklaSpeedtest speedtest = null!;
    private Clients.Testing.Server server = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Get, "*").Respond(request =>
        {
            var fileName = Path.GetFileName(request?.RequestUri?.AbsolutePath ?? "");

            var fileStream = new FileStream(Path.Combine("Payloads", fileName), FileMode.Open, FileAccess.Read, FileShare.Read,
                                    bufferSize: 64 * 1024,
                                    options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = fileStream.Length;

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        mockHttp.When(HttpMethod.Post, "*").Respond(async request =>
        {
            using var stream = await request.Content!.ReadAsStreamAsync();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            long total = 0;

            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });


        var httpClient = mockHttp.ToHttpClient();

        speedtest = new OoklaSpeedtest(null, httpClient);
        server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
    }

    [Benchmark] public Task Download() => speedtest.GetDownloadSpeedAsync(server);
    [Benchmark] public Task Upload() => speedtest.GetUploadSpeedAsync(server);
}
