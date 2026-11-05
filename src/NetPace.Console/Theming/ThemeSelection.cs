namespace NetPace.Console.Theming;

/// <summary>
/// The theme a run settled on, and the value it could not make sense of on the way there.
/// </summary>
/// <param name="Theme">The theme to draw in. Always a real theme, whatever the user typed.</param>
/// <param name="UnrecognisedValue">
/// What the user asked for when it named no theme, so the run can say so; <c>null</c> when they
/// asked for nothing, or for something that exists.
/// </param>
public sealed record ThemeSelection(Theme Theme, string? UnrecognisedValue);
