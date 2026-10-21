using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetPace.Console;
using NetPace.Console.Diagnostics;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NetPace.Console.Tests;

/// <summary>
/// Test host for System.CommandLine that captures output for testing.
/// </summary>
public sealed class CommandLineTestHost
{
    /// <summary>
    /// Spectre.Console's default buffer width - what it falls back to when stdout is not a terminal,
    /// so the width a piped or redirected run sees.
    /// </summary>
    public const int RedirectedWidth = 80;

    /// <summary>
    /// A width narrower than a machine-readable result line, standing in for a small terminal
    /// window. A width alone does not make a run interactive; pass <c>interactive</c> for that.
    /// </summary>
    public const int NarrowTerminalWidth = 40;

    /// <summary>
    /// A plausible terminal width for an interactive run: wide enough for a progress bar, its
    /// percentage and the live speed figure beside it without wrapping.
    /// </summary>
    public const int InteractiveTerminalWidth = 80;

    /// <summary>
    /// No width limit at all, so nothing the run writes can be wrapped by the console.
    /// </summary>
    public const int UnlimitedWidth = int.MaxValue;

    private readonly IServiceCollection serviceCollection;
    private readonly int consoleWidth;
    private readonly bool _interactive;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineTestHost"/> class.
    /// </summary>
    /// <param name="services">The service collection to use for dependency injection.</param>
    /// <param name="consoleWidth">
    /// The console width the run sees. Defaults to <see cref="UnlimitedWidth"/>, which keeps output
    /// unwrapped whatever the production code does; a test that cares how a narrow destination is
    /// handled passes <see cref="RedirectedWidth"/> or <see cref="NarrowTerminalWidth"/> instead.
    /// </param>
    /// <param name="interactive">
    /// Whether the run sees an interactive terminal, so the live progress display renders and the
    /// captured output carries the screen the user would watch. Defaults to <c>false</c>, the piped
    /// or redirected destination every other test here stands for. An interactive run needs a
    /// finite width: pass <see cref="InteractiveTerminalWidth"/> rather than leaving the default.
    /// </param>
    public CommandLineTestHost(IServiceCollection? services, int consoleWidth = UnlimitedWidth, bool interactive = false)
    {
        serviceCollection = services ?? new ServiceCollection();
        this.consoleWidth = consoleWidth;
        _interactive = interactive;
    }

    /// <summary>
    /// Runs the command with the specified arguments and captures the output.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A test result containing the exit code and output.</returns>
    public async Task<TestResult> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        using var testConsole = new TestConsole().Width(consoleWidth);

        if (_interactive)
        {
            testConsole.Interactive();
        }

        serviceCollection.TryAddSingleton<IAnsiConsole>(testConsole);

        // The diagnostic stream is captured separately from the result stream, so a test can assert
        // that each carries only its own content.
        using var diagnosticWriter = new StringWriter();
        serviceCollection.TryAddSingleton(new DiagnosticOutput(diagnosticWriter));

        // Default IClientInfoProvider stub unless a test already registered one
        serviceCollection.TryAddSingleton<IClientInfoProvider, ClientInfoProviderStub>();

        // Fixed stand-in values for the version, runtime, OS and architecture, and a counting clock
        // for record timestamps: all of them differ between machines, which would make a snapshot
        // machine-specific.
        serviceCollection.TryAddSingleton<IEnvironmentInfoProvider, EnvironmentInfoProviderStub>();
        serviceCollection.TryAddSingleton<IDiagnosticClock, IncrementingDiagnosticClockStub>();

        // Default OoklaSpeedtestSettingsAccessor (matches production DI). Tests that want to
        // inspect the bound settings register their own instance before calling RunAsync.
        serviceCollection.TryAddSingleton<OoklaSpeedtestSettingsAccessor>();

        await using var serviceProvider = serviceCollection.BuildServiceProvider();
        var exitCode = await Program.RunAsync(serviceProvider, args, cancellationToken);

        return new TestResult
        {
            ExitCode = exitCode,
            Output = testConsole.Output,
            DiagnosticOutput = diagnosticWriter.ToString(),
        };
    }
}

/// <summary>
/// Result from running a command in the test host.
/// </summary>
public sealed record TestResult
{
    /// <summary>
    /// Gets the exit code returned by the command.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets the output written to stdout.
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Gets what was written to the diagnostic stream — empty unless <c>--diagnostics</c> was passed.
    /// </summary>
    public required string DiagnosticOutput { get; init; }
}
