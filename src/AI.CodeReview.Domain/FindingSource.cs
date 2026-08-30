namespace AI.CodeReview.Domain;

/// <summary>Which review pipeline stage produced a finding.</summary>
public enum FindingSource
{
    StaticAnalysis,
    Ai
}
