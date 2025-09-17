using NetPace.Console;
using NetPace.Console.DependencyInjection;
using NetPace.Core;
using NetPace.Core.Clients.Ookla;

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
    internal static Action<IConfigurator> ConfigureAction = (config =>
    {
        config.SetApplicationName("NetPace");
        config.ValidateExamples();
        config.Settings.ShowOptionDefaultValues = true;
        config.Settings.TrimTrailingPeriod = false;

        // Register the custom help provider
        config.SetHelpProvider(new CustomHelpProvider(config.Settings));

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

    public static int Main(string[] args)
    {
        var registrar = new TypeRegistrar();

        var cancellationTokenSource = new CancellationTokenSource();
        registrar.RegisterInstance(typeof(CancellationToken), cancellationTokenSource.Token);

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
            eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
            cancellationTokenSource.Cancel();
        };

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

        var app = GetCommandApp(registrar);
        var result = app.Run(args ?? Array.Empty<string>());
        return result;
    }
}