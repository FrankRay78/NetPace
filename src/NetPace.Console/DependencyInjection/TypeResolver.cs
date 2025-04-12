namespace NetPace.Console.DependencyInjection;

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider provider;

    public TypeResolver(IServiceProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        return provider.GetService(type);
    }

    public void Dispose()
    {
        // When an exception is thrown within a Spectre.Console commamnd that is under test
        // and exceptions are handled by Spectre.Console rather than propagated to the caller,
        // leaving the using block inside CommandExecutor will result in TypeResolver.Dispose
        // being called, disposing of the StringWriter within the TestConsole instance that
        // Spectre.Console later relies upon to write the formatted exception to the console.
        // Below is an acceptable workaround that prevents the StringWriter from being disposed.

        // The StringWriter doesn't actually hold any unmanaged resources that require explicit disposal.
        // https://learn.microsoft.com/en-us/dotnet/api/system.io.stringwriter?view=net-9.0

        //if (provider is IDisposable disposable)
        //{
        //    disposable.Dispose();
        //}
    }
}
