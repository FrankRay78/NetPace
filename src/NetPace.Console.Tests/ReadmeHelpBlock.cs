using System.Text;

namespace NetPace.Console.Tests;

/// <summary>
/// The <c>netpace --help</c> output that <c>README.md</c> reproduces, and the comparison that
/// keeps it honest. The README is the first description of the CLI most people see, and its help
/// block is edited by hand, so this is what catches one left behind by a change to the help.
/// </summary>
internal static class ReadmeHelpBlock
{
    /// <summary>
    /// The illustrative prompt line the README block opens with. It is not part of the help
    /// output, so it both locates the block and is excluded from the comparison.
    /// </summary>
    public const string PromptLine = @"C:\>netpace.exe --help";

    private const string FenceOpen = "```txt";
    private const string FenceClose = "```";

    /// <summary>
    /// Reads <c>README.md</c> from beside the test assembly, where the project file copies it.
    /// </summary>
    public static string ReadReadme()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "README.md");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"README.md was not copied next to the test assembly ({path}). " +
                "NetPace.Console.Tests.csproj copies the repository README into the test output.",
                path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Extracts the help output the README reproduces: the contents of the fenced block that
    /// opens with <see cref="PromptLine"/>, with that prompt line removed. The blank line after
    /// the prompt line is kept: it reproduces the help output's own blank first line, which is
    /// what aligns the two for comparison.
    /// </summary>
    /// <param name="markdown">The README contents.</param>
    /// <returns>The reproduced help output, one line per element, trailing whitespace removed.</returns>
    public static IReadOnlyList<string> Extract(string markdown)
    {
        var lines = SplitLines(markdown);
        var start = -1;

        // Markers are matched with trailing whitespace removed, as the compared lines are: a stray
        // space renders identically, and would otherwise hide the block or run it on into the
        // next fenced block.
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].TrimEnd() == FenceOpen && lines[i + 1].TrimEnd() == PromptLine)
            {
                start = i + 2;
                break;
            }
        }

        if (start < 0)
        {
            throw new InvalidOperationException(
                $"No README help block was found. It is the '{FenceOpen}' fenced block whose first line is '{PromptLine}'.");
        }

        var end = Array.FindIndex(lines, start, line => line.TrimEnd() == FenceClose);

        if (end < 0)
        {
            throw new InvalidOperationException(
                $"The README help block opened with '{FenceOpen}' but was never closed with '{FenceClose}'.");
        }

        return Trimmed(lines[start..end]);
    }

    /// <summary>
    /// Splits captured help output into comparable lines.
    /// </summary>
    public static IReadOnlyList<string> Normalise(string helpOutput) => Trimmed(SplitLines(helpOutput));

    /// <summary>
    /// Compares the README's reproduction against the real help output.
    /// </summary>
    /// <param name="readme">The lines <see cref="Extract"/> returned.</param>
    /// <param name="help">The lines <see cref="Normalise"/> returned.</param>
    /// <returns>
    /// A report naming every differing line, or <c>null</c> when the two agree.
    /// </returns>
    /// <remarks>
    /// The two are aligned on their longest common subsequence of lines rather than by position,
    /// so an option added to or removed from the middle of the help is reported as that one line,
    /// not as every line after it.
    /// </remarks>
    public static string? Compare(IReadOnlyList<string> readme, IReadOnlyList<string> help)
    {
        // common[i, j] is the length of the longest common subsequence of readme[i..] and help[j..].
        var common = new int[readme.Count + 1, help.Count + 1];

        for (var i = readme.Count - 1; i >= 0; i--)
        {
            for (var j = help.Count - 1; j >= 0; j--)
            {
                common[i, j] = readme[i] == help[j]
                    ? common[i + 1, j + 1] + 1
                    : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        var report = new StringBuilder();
        var r = 0;
        var h = 0;

        while (r < readme.Count || h < help.Count)
        {
            if (r < readme.Count && h < help.Count && readme[r] == help[h])
            {
                r++;
                h++;
            }
            else if (r < readme.Count && (h == help.Count || common[r + 1, h] >= common[r, h + 1]))
            {
                // Line numbers count from the line after the prompt line, where the compared
                // block starts.
                report.AppendLine($"- README.md line {r + 1}: {readme[r]}");
                r++;
            }
            else
            {
                report.AppendLine($"+ --help    line {h + 1}: {help[h]}");
                h++;
            }
        }

        if (report.Length == 0)
        {
            return null;
        }

        return
            "The README.md help block no longer matches what 'netpace --help' prints. " +
            "Lines marked '-' are in README.md but not printed by --help; lines marked '+' are " +
            "printed by --help but missing from README.md. Update README.md to match, then re-run." +
            Environment.NewLine +
            report.ToString();
    }

    /// <summary>
    /// Splits on any line ending, so the comparison is unaffected by the line endings a checkout
    /// or a platform's console uses, and removes a byte order mark.
    /// </summary>
    private static string[] SplitLines(string text) =>
        text.TrimStart('\uFEFF').Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

    /// <summary>
    /// Removes trailing whitespace from every line — the help grid pads its last column — and
    /// drops the empty lines a fence or a final newline leaves at the end.
    /// </summary>
    private static List<string> Trimmed(IEnumerable<string> lines)
    {
        var trimmed = lines.Select(line => line.TrimEnd()).ToList();

        while (trimmed.Count > 0 && trimmed[^1].Length == 0)
        {
            trimmed.RemoveAt(trimmed.Count - 1);
        }

        return trimmed;
    }
}
