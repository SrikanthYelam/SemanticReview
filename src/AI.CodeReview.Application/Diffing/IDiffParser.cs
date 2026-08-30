namespace AI.CodeReview.Application.Diffing;

/// <summary>
/// Parses a unified diff into per-file added lines. Deleted-only lines are ignored;
/// this is intentionally a small, isolated component covering standard unified diffs
/// rather than a complete git diff/patch parser.
/// </summary>
public interface IDiffParser
{
    IReadOnlyList<ParsedFileDiff> Parse(string diffText);
}
