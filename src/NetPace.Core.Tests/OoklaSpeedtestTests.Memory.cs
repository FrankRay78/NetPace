using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    public sealed class Memory
    {
        [Fact]
        public async Task Franks_Unit_Test()
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

                Console.WriteLine($"Get: {request?.RequestUri}, {fileStream.Length} bytes");

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

                Console.WriteLine($"Post: {request.RequestUri}, {total} bytes");

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            var settings = new OoklaSpeedtestSettings
            {
                DownloadTest = new()
                {
                    DownloadParallelTasks = 1
                }
            };


            var httpClient = mockHttp.ToHttpClient();

            var speedtest = new OoklaSpeedtest(null, httpClient);
            var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            await speedtest.GetDownloadSpeedAsync(server);
            await speedtest.GetUploadSpeedAsync(server);

            // Then
        }
    }
}
