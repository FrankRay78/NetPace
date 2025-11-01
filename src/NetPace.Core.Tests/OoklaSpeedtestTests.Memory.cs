using System.Buffers;
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
        public async Task OoklaSpeedtest_Should_Remain_In_Reasonable_Memory_Limits()
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

            var speedtest = new OoklaSpeedtest(null, httpClient);
            var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test Sponsor", Location = "Test Location" };


            // When
            // Then
            await ExecuteAndAssertMemoryUsage(async () =>
            {
                await speedtest.GetDownloadSpeedAsync(server);
                await speedtest.GetUploadSpeedAsync(server);
            },
            runs: 10,
            thresholdAllocatedBytes: 100 * 1024 * 1024); // 100 MB
        }

        private async Task ExecuteAndAssertMemoryUsage(Func<Task> methodToTest, int runs, long thresholdAllocatedBytes)
        {
            long maxAllocatedBytes = 0;

            // Initial cleanup before starting.
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            for (int i = 0; i < runs; i++)
            {
                var before = GC.GetTotalAllocatedBytes(precise: true);

                // Method under test.
                await methodToTest();

                var after = GC.GetTotalAllocatedBytes(precise: true);
                var allocated = after - before;

                maxAllocatedBytes = Math.Max(maxAllocatedBytes, allocated);

                // Force cleanup between runs.
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

                // Assert each run to avoid unnecessary continuation.
                maxAllocatedBytes.ShouldBeLessThan(thresholdAllocatedBytes,
                    $"Memory exceeded threshold. Max: {maxAllocatedBytes:N0} bytes");
            }

            Console.WriteLine($"Maximum allocated bytes: {maxAllocatedBytes:N0} bytes");
        }
    }
}
