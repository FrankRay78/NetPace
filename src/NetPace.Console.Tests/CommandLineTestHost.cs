using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetPace.Console;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NetPace.Console.Tests;

/// <summary>
/// Test host for System.CommandLine that captures output for testing.
/// </summary>
public sealed class CommandLineTestHost
{
    private readonly IServiceCollection serviceCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineTestHost"/> class.
    /// </summary>
    /// <param name="services">The service collection to use for dependency injection.</param>
    public CommandLineTestHost(IServiceCollection? services)
    {
        serviceCollection = services ?? new ServiceCollection();
    }

    /// <summary>
    /// Runs the command with the specified arguments and captures the output.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A test result containing the exit code and output.</returns>
    public async Task<TestResult> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        // Register AnsiConsole with maximum width to prevent text wrapping
        using var testConsole = new TestConsole().Width(int.MaxValue);
        serviceCollection.TryAddSingleton<IAnsiConsole>(testConsole);

        // Default IClientInfoProvider stub unless a test already registered one
        serviceCollection.TryAddSingleton<IClientInfoProvider, ClientInfoProviderStub>();

        // Default OoklaSpeedtestSettingsAccessor (matches production DI). Tests that want to
        // inspect the bound settings register their own instance before calling RunAsync.
        serviceCollection.TryAddSingleton<OoklaSpeedtestSettingsAccessor>();

        await using var serviceProvider = serviceCollection.BuildServiceProvider();
        var exitCode = await Program.RunAsync(serviceProvider, args, cancellationToken);

        return new TestResult
        {
            ExitCode = exitCode,
            Output = testConsole.Output,
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
}
