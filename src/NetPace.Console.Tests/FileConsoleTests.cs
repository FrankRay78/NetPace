using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetPace.Console.Tests;

/// <summary>
/// Unit tests for FileConsole to verify it writes plain text without ANSI formatting.
/// </summary>
public sealed class FileConsoleTests : IDisposable
{
    private readonly string _testFilePath;

    public FileConsoleTests()
    {
        // Create a unique temp file for each test
        _testFilePath = Path.Join(Path.GetTempPath(), $"fileconsole-test-{Guid.NewGuid()}.txt");
    }

    public void Dispose()
    {
        // Clean up test file
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Should_Strip_Markup_When_Writing_To_File()
    {
        // Given
        using (var fileConsole = new FileConsole(_testFilePath, FileMode.Overwrite))
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
        using var fileConsole = new FileConsole(_testFilePath, FileMode.Overwrite);

        // When / Then
        fileConsole.WriteAnsi(_ => { });
    }
}
