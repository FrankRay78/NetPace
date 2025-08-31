using System.Reflection;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

internal class CustomHelpProvider : HelpProvider
{
    private readonly string? ApplicationName;

    public CustomHelpProvider(ICommandAppSettings settings)
        : base(settings)
    {
        ApplicationName = settings.ApplicationName;
    }

    public override IEnumerable<IRenderable> GetHeader(ICommandModel model, ICommandInfo? command)
    {
        if (!string.IsNullOrWhiteSpace(ApplicationName))
        {
            var font = LoadEmbeddedFont("slant.flf");

            return
            [
                Text.NewLine,
                new FigletText(font, ApplicationName)
                    .LeftJustified()
                    .Color(Color.Gold1),
                Text.NewLine
            ];
        }

        return base.GetHeader(model, command);
    }

    private static FigletFont LoadEmbeddedFont(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"NetPace.Console.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        return FigletFont.Load(stream);
    }
}