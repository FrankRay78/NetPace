using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetPace.Console.Tests;

public sealed class FileConsoleTests : IDisposable
{
    private readonly string _testFilePath;

    public FileConsoleTests()
    {
        _testFilePath = Path.Join(Path.GetTempPath(), $"fileconsole-test-{Guid.NewGuid()}.txt");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Should_Strip_Markup_When_Writing_To_File()
    {
        // Given
        using (var fileConsole = new FileConsole(_testFilePath, FileMode.Overwrite, startedEmpty: true))
        {
            // When
            fileConsole.Write(new Markup("[red]Error:[/] Something went wrong"));
        }

        // Then
        var fileContent = File.ReadAllText(_testFilePath);
        Assert.Equal("Error: Something went wrong", fileContent);
    }

    [Fact]
    public void WriteAnsi_DoesNotThrow_ForFileConsole()
    {
        // Given
        using var fileConsole = new FileConsole(_testFilePath, FileMode.Overwrite, startedEmpty: true);

        // When / Then
        fileConsole.WriteAnsi(_ => { });
    }

    [Fact]
    public void Write_AfterDispose_DoesNotThrow()
    {
        // Given — simulates Spectre.Console's ProgressRefreshThread firing after disposal
        var fileConsole = new FileConsole(_testFilePath, FileMode.Overwrite, startedEmpty: true);
        fileConsole.Dispose();

        // When / Then
        fileConsole.Write(new Markup("late write"));
    }
}
