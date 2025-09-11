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

    public override IEnumerable<IRenderable> GetFooter(ICommandModel model, ICommandInfo? command)
    {
        // User Guide link.
        const string userGuideUrl = "https://github.com/FrankRay78/NetPace/USER_GUIDE.md";

        // Renderable for SEE ALSO section.
        var seeAlsoText = new Markup($"\n[bold yellow]SEE ALSO:[/]\n    [link={userGuideUrl}]{userGuideUrl}[/]\n");

        // Combine base footer and SEE ALSO section.
        var baseFooter = base.GetFooter(model, command);
        return baseFooter.Concat(new[] { seeAlsoText });
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