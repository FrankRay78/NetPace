using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// The public surface of <see cref="NoServersAvailableException"/>, which ships to NuGet
/// consumers: the message NetPace throws it with, and the two forms a consumer re-throwing the
/// condition with its own context can use.
/// </summary>
public sealed class NoServersAvailableExceptionTests
{
    [Fact]
    public void Constructor_WithNoArguments_CarriesTheMessageNetPaceReports()
    {
        // Given, When the form every NetPace component throws.
        var exception = new NoServersAvailableException();

        // Then it carries the line the console prints after its "Error:" prefix, and nothing is
        // claimed about a cause.
        exception.Message.ShouldBe("No servers available");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithAMessage_CarriesThatMessageInstead()
    {
        // Given, When a consumer re-throws the condition with its own context.
        var exception = new NoServersAvailableException("Every candidate timed out after 5s.");

        // Then its message replaces NetPace's rather than being appended to it.
        exception.Message.ShouldBe("Every candidate timed out after 5s.");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithAMessageAndCause_PreservesBoth()
    {
        // Given a cause a consumer wants to keep.
        var cause = new TimeoutException("Screening timed out.");

        // When
        var exception = new NoServersAvailableException("Every candidate timed out after 5s.", cause);

        // Then both survive, so the cause is not lost on the way to the consumer's handler.
        exception.Message.ShouldBe("Every candidate timed out after 5s.");
        exception.InnerException.ShouldBeSameAs(cause);
    }
}
