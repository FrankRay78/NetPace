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
        //var registrar = new TypeRegistrar();
        //registrar.RegisterInstance(typeof(ISpeedTestService), new SpeedTestStub(250));
        //registrar.Register(typeof(IClock), typeof(ClockStub));

        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(OoklaSpeedtest));
        registrar.Register(typeof(IClock), typeof(Clock));

        var app = GetCommandApp(registrar);
        var result = app.Run(args);
        return result;
    }
}