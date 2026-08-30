namespace AI.CodeReview.Application.Diffing;

/// <summary>An added line's 1-based line number in the *new* version of the file, and its content.</summary>
public readonly record struct DiffAddedLine(int LineNumber, string Content);
