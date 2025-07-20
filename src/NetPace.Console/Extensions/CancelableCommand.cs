namespace Spectre.Console.Extensions;

public abstract class CancelableCommand(CancellationToken cancellationToken) : CancelableCommand<EmptyCommandSettings>(cancellationToken)
{
    protected override async Task<int> ExecuteAsync(CommandContext commandContext, EmptyCommandSettings settings, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(commandContext, cancellationToken);
    }

    protected abstract Task<int> ExecuteAsync(CommandContext commandContext, CancellationToken cancellationToken);
}

public abstract class CancelableCommand<TSettings>(CancellationToken cancellationToken) : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    public override async Task<int> ExecuteAsync(CommandContext commandContext, TSettings settings)
    {
        return await ExecuteAsync(commandContext, settings, cancellationToken);
    }

    protected abstract Task<int> ExecuteAsync(CommandContext commandContext, TSettings settings, CancellationToken cancellationToken);
}