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
            var font = FigletFont.Load("slant.flf");

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
}