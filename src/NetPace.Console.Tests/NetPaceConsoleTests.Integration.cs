using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Integration tests that run the actual NetPace executable as a process.
    /// These tests catch differences between CommandAppTester behavior and real console output.
    /// </summary>
    public sealed class Integration
    {
        [Fact]
        public async Task Should_Run_NetPace_Test_Command_As_Process()
        {
            // Given
            var exePath = GetNetPaceExecutablePath();

            // When
            var output = await RunNetPaceProcessAsync(exePath, "--test", "--no-upload", "--no-download");

            // Then
            await Verify(output);
        }

        /// <summary>
        /// Gets the path to the NetPace executable based on the current build configuration and OS.
        /// </summary>
        private static string GetNetPaceExecutablePath()
        {
            // Get the current test assembly's directory
            var testAssemblyPath = typeof(NetPaceConsoleTests).Assembly.Location;
            var testBinDirectory = Path.GetDirectoryName(testAssemblyPath)!;

            // Navigate to the NetPace.Console bin directory
            // From: src/NetPace.Console.Tests/bin/Debug/net8.0/
            // To:   src/NetPace.Console/bin/Debug/net8.0/
            var consoleBinDirectory = testBinDirectory.Replace(
                Path.Combine("NetPace.Console.Tests", "bin"),
                Path.Combine("NetPace.Console", "bin"));

            // Use platform-specific executable name
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "NetPace.exe"
                : "NetPace";

            var exePath = Path.Combine(consoleBinDirectory, exeName);

            // Ensure the executable exists (in case tests run before build)
            if (!System.IO.File.Exists(exePath))
            {
                throw new FileNotFoundException("File not found", exePath);
            }

            return exePath;
        }

        /// <summary>
        /// Runs the NetPace executable as a process and captures its output.
        /// </summary>
        private static async Task<string> RunNetPaceProcessAsync(string exePath, params string[] arguments)
        {
            var output = new System.Text.StringBuilder();

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = string.Join(" ", arguments),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return output.ToString();
        }
    }
}
