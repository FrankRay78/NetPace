using System.Text;
using System.Text.Json;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// What a <c>--file</c> target holds as bytes, rather than as text. The rest of the suite reads
    /// a written file back with <c>ReadAllTextAsync</c>, which strips a byte-order mark before the
    /// test ever sees it, so these tests read the bytes themselves — the view a strict JSON parser,
    /// a Unix text tool and a CSV reader all take.
    /// </summary>
    public sealed class FileEncoding
    {
        private static readonly byte[] Utf8ByteOrderMark = Encoding.UTF8.GetPreamble();

        /// <summary>UTF-8 as a file is written: no byte-order mark in front of the content.</summary>
        private static readonly UTF8Encoding Utf8NoByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

        /// <summary>UTF-8 that refuses to decode a byte sequence that is not valid UTF-8.</summary>
        private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // SCENARIO: A JSON result written to a file parses as strict JSON
        [Fact]
        public async Task Json_WrittenToANewFile_IsReadByAStrictJsonParser()
        {
            // Given a path nothing has written to yet
            var file = NewFilePath();

            try
            {
                // When a run writes its result there as JSON
                var (_, bytes) = await RunToFileAsync(file, ["--json"]);

                // Then a parser handed the file's bytes exactly as they are reads it without error.
                // Anything in front of the document — a byte-order mark above all — leaves text no
                // strict parser accepts, so the parse is the whole assertion and the document is
                // discarded unread: what the output says is covered by the JSON snapshots.
                JsonDocument.Parse(bytes).Dispose();
            }
            finally
            {
                Delete(file);
            }
        }

        // SCENARIO: A CSV result written to a file starts with its header
        [Fact]
        public async Task Csv_WrittenToANewFile_HoldsTheSameBytesAsStandardOutput()
        {
            // Given a path nothing has written to yet
            var file = NewFilePath();

            try
            {
                // When a run writes its result there as CSV, and to standard output as well
                var (output, bytes) = await RunToFileAsync(file, ["--csv"]);

                // Then the file is byte for byte what the same run put on standard output, so it
                // begins with the first header column name itself and `cut`, `awk` or `head` see
                // the same first field either way.
                Assert.Equal(Utf8NoByteOrderMark.GetBytes(output), bytes);
            }
            finally
            {
                Delete(file);
            }
        }

        // SCENARIO: Non-ASCII text in a file stays UTF-8
        [Fact]
        public async Task NonAsciiServerName_WrittenToANewFile_IsEncodedAsUtf8()
        {
            // Given a server whose sponsor and location carry non-ASCII characters
            const string Location = "Zürich";
            const string Sponsor = "Télécom Köln 日本";

            var tester = new ScriptedSpeedTester
            {
                Server = new Server { Location = Location, Sponsor = Sponsor, Url = ScriptedSpeedTester.Url },
            };

            var file = NewFilePath();

            try
            {
                // When the result is written to a file
                var (_, bytes) = await RunToFileAsync(file, ["--csv"], tester);

                // Then the file decodes as UTF-8 — a byte that is not valid UTF-8 throws here —
                // and carries both names as the server gave them.
                var text = StrictUtf8.GetString(bytes);

                Assert.Contains(Location, text, StringComparison.Ordinal);
                Assert.Contains(Sponsor, text, StringComparison.Ordinal);
            }
            finally
            {
                Delete(file);
            }
        }

        [Theory]
        [InlineData(null, "Overwrite")]
        [InlineData(null, "Append")]
        [InlineData("--minimal", "Overwrite")]
        [InlineData("--minimal", "Append")]
        [InlineData("--simple", "Overwrite")]
        [InlineData("--simple", "Append")]
        [InlineData("--csv", "Overwrite")]
        [InlineData("--csv", "Append")]
        [InlineData("--json", "Overwrite")]
        [InlineData("--json", "Append")]
        [InlineData("--json-pretty", "Overwrite")]
        [InlineData("--json-pretty", "Append")]
        public async Task EveryFormat_WrittenToANewFile_LeavesNoByteOrderMark(string? formatSwitch, string fileMode)
        {
            // Given a path nothing has written to yet
            var file = NewFilePath();

            try
            {
                // When a run writes its result there in this format and file mode
                string[] args = formatSwitch is null
                    ? ["--file-mode", fileMode]
                    : [formatSwitch, "--file-mode", fileMode];

                var (_, bytes) = await RunToFileAsync(file, args);

                // Then
                AssertNoByteOrderMark(bytes);
            }
            finally
            {
                Delete(file);
            }
        }

        [Fact]
        public async Task Csv_AppendedToALogHoldingOnlyAByteOrderMark_LeavesNoByteOrderMark()
        {
            // Given a log holding nothing but a byte-order mark, which is what an older NetPace
            // left when a run ended before writing a row
            var file = NewFilePath();

            try
            {
                await System.IO.File.WriteAllBytesAsync(file, Utf8ByteOrderMark);

                // When a run appends its result
                var (_, bytes) = await RunToFileAsync(file, ["--csv", "--file-mode", "Append"]);

                // Then the stale mark is gone: such a log counts as holding nothing, so it is
                // replaced rather than appended to. Read back as text the mark is invisible, which
                // is why this reads the bytes.
                AssertNoByteOrderMark(bytes);
            }
            finally
            {
                Delete(file);
            }
        }

        [Fact]
        public async Task Csv_AppendedToALogWithRows_AddsNoByteOrderMark()
        {
            // Given a log that already holds content, so this run appends to it rather than
            // replacing it
            var file = NewFilePath();

            try
            {
                await System.IO.File.WriteAllBytesAsync(file, "EARLIER RUN\n"u8.ToArray());

                // When a run appends its result
                var (_, bytes) = await RunToFileAsync(file, ["--csv", "--file-mode", "Append"]);

                // Then the appended content adds no byte-order mark of its own. A mark the file
                // already carries is left alone by design, which is why the log seeded here has
                // none.
                AssertNoByteOrderMark(bytes);
            }
            finally
            {
                Delete(file);
            }
        }

        private static string NewFilePath() =>
            Path.Join(Path.GetTempPath(), $"netpace-file-encoding-{Guid.NewGuid()}.txt");

        private static void Delete(string file)
        {
            if (System.IO.File.Exists(file))
            {
                System.IO.File.Delete(file);
            }
        }

        /// <summary>
        /// Runs NetPace with <c>--file</c> pointed at <paramref name="file"/>, and returns both what
        /// the run wrote to standard output and the bytes it left on disk.
        /// </summary>
        private static async Task<(string Output, byte[] FileBytes)> RunToFileAsync(string file, string[] args, ISpeedTestService? speedTestService = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(speedTestService ?? new SpeedTestStub());
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            var result = await new CommandLineTestHost(services).RunAsync([.. args, "--file", file]);

            Assert.Equal(0, result.ExitCode);

            return (result.Output, await System.IO.File.ReadAllBytesAsync(file));
        }

        private static void AssertNoByteOrderMark(byte[] bytes)
        {
            int at = bytes.AsSpan().IndexOf(Utf8ByteOrderMark);

            Assert.True(at < 0, $"A UTF-8 byte-order mark is present at byte {at} of {bytes.Length}.");
        }
    }
}
