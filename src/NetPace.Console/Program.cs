using System.Reflection;
using NetPace.Console;
using NetPace.Console.DependencyInjection;
using NetPace.Core;

public static class Program
{
    /// <summary>
    /// The application description
    /// </summary>
    internal const string Description = "Network speed tester including server discovery, latency measurement, download and upload speed testing.";

    /// <summary>
    /// The configure action for the CommandApp.
    /// </summary>
    /// <remarks>
    /// Extracted here so the testing project can reuse the production configuration.
    /// </remarks>
    internal static readonly Action<IConfigurator> ConfigureAction = (config =>
    {
        config.SetApplicationName("NetPace");
        config.ValidateExamples();
        config.Settings.ShowOptionDefaultValues = true;
        config.Settings.TrimTrailingPeriod = false;

        // Register the custom help provider
        config.SetHelpProvider(new CustomHelpProvider(config.Settings));

        // Set application version for Spectre.Console automatic version handling
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
            ?? assembly.GetName().Version?.ToString()
            ?? "N/A";
        config.SetApplicationVersion(version);

        config.AddCommand<ListServersCommand>("servers")
            .WithDescription("Show the nearest speed test servers.");
    });

    /// <summary>
    /// Create the CommandApp and configure.
    /// </summary>
    private static ICommandApp GetCommandApp(ITypeRegistrar registrar)
    {
        var app = new CommandApp<SpeedTestCommand>(registrar)
            .WithDescription(Description);

        app.Configure(ConfigureAction);

        return app;
    }

    public async static Task<int> Main(string[] args)
    {
        var registrar = new TypeRegistrar();

        if (args != null && args.Contains("--test"))
        {
            // Executes NetPace against stub service implementations.
            registrar.RegisterInstance(typeof(ISpeedTestService), new SpeedTestStub(250));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        }
        else
        {
            registrar.Register(typeof(ISpeedTestService), typeof(OoklaSpeedtest));
            registrar.Register(typeof(IClock), typeof(Clock));
            registrar.Register(typeof(IWaiter), typeof(Waiter));
        }

        var cancellationTokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
            eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
            cancellationTokenSource.Cancel();
        };

        var app = GetCommandApp(registrar);
        var result = await app.RunAsync(args ?? Array.Empty<string>(), cancellationTokenSource.Token);
        return result;
    }
}