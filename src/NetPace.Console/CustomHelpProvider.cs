using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

internal class CustomHelpProvider : HelpProvider
{
    /// <summary>
    /// Used to provide a description for the application root.
    /// </summary>
    private class DummyCommand : ICommandInfo
    {
        public string Name => throw new NotImplementedException();

        public string? Description { get; set; }

        public bool IsBranch => throw new NotImplementedException();

        public bool IsDefaultCommand => throw new NotImplementedException();

        public bool IsHidden => throw new NotImplementedException();

        public IReadOnlyList<ICommandParameter> Parameters => throw new NotImplementedException();

        public ICommandInfo? Parent => throw new NotImplementedException();

        public IReadOnlyList<string[]> Examples => throw new NotImplementedException();

        public IReadOnlyList<ICommandInfo> Commands => throw new NotImplementedException();

        public ICommandInfo? DefaultCommand => throw new NotImplementedException();
    }

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