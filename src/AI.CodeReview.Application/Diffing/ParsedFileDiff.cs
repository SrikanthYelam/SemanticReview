namespace AI.CodeReview.Application.Diffing;

public sealed record ParsedFileDiff(string FileName, IReadOnlyList<DiffAddedLine> AddedLines);
