using System.Text.RegularExpressions;
using AI.CodeReview.Application.Diffing;

namespace AI.CodeReview.Infrastructure.Diffing;

/// <summary>
/// Parses standard unified diffs (as produced by `git diff`). Supports the common subset:
/// file headers ("diff --git" / "+++ b/&lt;file&gt;") and hunk headers ("@@ -a,b +c,d @@"),
/// tracking new-file line numbers for added ("+") lines. Deleted ("-") and context (" ") lines
/// are used only to keep the new-file line counter accurate; deleted-only content is discarded.
/// </summary>
public sealed class UnifiedDiffParser : IDiffParser
{
    private static readonly Regex HunkHeaderRegex = new(
        @"^@@ -\d+(?:,\d+)? \+(?<newStart>\d+)(?:,\d+)? @@",
        RegexOptions.Compiled);

    public IReadOnlyList<ParsedFileDiff> Parse(string diffText)
    {
        var results = new List<ParsedFileDiff>();
        if (string.IsNullOrWhiteSpace(diffText))
        {
            return results;
        }

        string? currentFileName = null;
        List<DiffAddedLine>? currentAddedLines = null;
        int newLineCounter = 0;
        var inHunk = false;

        void FlushCurrentFile()
        {
            if (currentFileName is not null)
            {
                results.Add(new ParsedFileDiff(currentFileName, currentAddedLines ?? new List<DiffAddedLine>()));
            }
        }

        var lines = diffText.Replace("\r\n", "\n").Split('\n');

        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                FlushCurrentFile();
                currentFileName = null;
                currentAddedLines = null;
                inHunk = false;
                continue;
            }

            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                FlushCurrentFile();
                var path = line["+++ ".Length..].Trim();
                inHunk = false;

                if (path == "/dev/null")
                {
                    // File was deleted — nothing new to review.
                    currentFileName = null;
                    currentAddedLines = null;
                    continue;
                }

                currentFileName = StripGitPrefix(path);
                currentAddedLines = new List<DiffAddedLine>();
                continue;
            }

            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                // Old-file path; not needed since only additions to the new file are reviewed.
                continue;
            }

            var hunkMatch = HunkHeaderRegex.Match(line);
            if (hunkMatch.Success)
            {
                newLineCounter = int.Parse(hunkMatch.Groups["newStart"].Value);
                inHunk = true;
                continue;
            }

            if (!inHunk || currentFileName is null)
            {
                continue;
            }

            if (line.StartsWith('+'))
            {
                currentAddedLines!.Add(new DiffAddedLine(newLineCounter, line[1..]));
                newLineCounter++;
            }
            else if (line.StartsWith('-'))
            {
                // Deletion — does not exist in the new file, so it does not consume a new-line number.
            }
            else if (line.StartsWith('\\'))
            {
                // e.g. "\ No newline at end of file" — not a content line.
            }
            else
            {
                // Context line (normally prefixed with a single space).
                newLineCounter++;
            }
        }

        FlushCurrentFile();

        return results;
    }

    private static string StripGitPrefix(string path)
    {
        if (path.Length > 2 && (path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal)))
        {
            return path[2..];
        }

        return path;
    }
}
