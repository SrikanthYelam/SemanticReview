using AI.CodeReview.Application.Analysis;
using AI.CodeReview.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AI.CodeReview.Infrastructure.Roslyn;

/// <summary>
/// Deterministic C# checks that don't require an LLM: empty catch blocks, blocking-async
/// patterns (.Result/.Wait()), leftover Debugger calls, TODO comments, and overly long methods.
/// <para>
/// This analyzes the diff's added-line fragment directly (not a full compilable file), so it is
/// syntactic best-effort analysis — Roslyn's parser is error-tolerant and still produces usable
/// nodes for fragments like a lone catch block or method body, which is sufficient for these
/// checks, but there is no semantic model / symbol binding available.
/// </para>
/// </summary>
public sealed class RoslynStaticAnalyzer : IStaticCodeAnalyzer
{
    private const int LongMethodLineThreshold = 50;

    public IReadOnlyList<ReviewFinding> Analyze(string fileName, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Array.Empty<ReviewFinding>();
        }

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        var findings = new List<ReviewFinding>();

        var walker = new FindingWalker(fileName, findings);
        walker.Visit(root);

        AnalyzeTodoComments(fileName, root, findings);

        return findings;
    }

    private static void AnalyzeTodoComments(string fileName, SyntaxNode root, List<ReviewFinding> findings)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.Kind() is not (SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia))
            {
                continue;
            }

            var text = trivia.ToString();
            if (text.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            {
                var line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                findings.Add(new ReviewFinding(
                    Severity.Info,
                    ReviewCategory.CodeQuality,
                    fileName,
                    line,
                    "TODO comment left in code.",
                    "Resolve the TODO or track it in an issue tracker before merging.",
                    FindingSource.StaticAnalysis));
            }
        }
    }

    private sealed class FindingWalker : CSharpSyntaxWalker
    {
        private readonly string _fileName;
        private readonly List<ReviewFinding> _findings;

        public FindingWalker(string fileName, List<ReviewFinding> findings)
        {
            _fileName = fileName;
            _findings = findings;
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            if (node.Block is { Statements.Count: 0 })
            {
                Add(node.Block.GetLocation(), Severity.Medium, ReviewCategory.ExceptionHandling,
                    "Catch block is empty; the exception is silently swallowed.",
                    "Handle the exception meaningfully (log it, rethrow, or take corrective action), and prefer catching specific exception types.");
            }

            base.VisitCatchClause(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.Identifier.Text == "Result")
            {
                Add(node.Name.GetLocation(), Severity.High, ReviewCategory.Async,
                    "Synchronous access to Task.Result can cause deadlocks and blocks the calling thread.",
                    "Await the task instead of accessing .Result.");
            }

            base.VisitMemberAccessExpression(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.Text == "Wait" && node.ArgumentList.Arguments.Count == 0)
                {
                    Add(memberAccess.Name.GetLocation(), Severity.High, ReviewCategory.Async,
                        ".Wait() blocks the calling thread and can cause deadlocks.",
                        "Await the task instead of calling .Wait().");
                }

                var target = memberAccess.Expression.ToString();
                if (target is "Debugger" or "System.Diagnostics.Debugger" &&
                    memberAccess.Name.Identifier.Text is "Break" or "Launch")
                {
                    Add(node.GetLocation(), Severity.Medium, ReviewCategory.CodeQuality,
                        $"Debugger.{memberAccess.Name.Identifier.Text}() call left in code will interrupt execution or attach a debugger.",
                        "Remove the Debugger invocation before merging.");
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;

            if (lineCount > LongMethodLineThreshold)
            {
                Add(node.Identifier.GetLocation(), Severity.Low, ReviewCategory.Maintainability,
                    $"Method '{node.Identifier.Text}' is {lineCount} lines long, which may hurt readability and testability.",
                    "Consider breaking this method into smaller, more focused methods.");
            }

            base.VisitMethodDeclaration(node);
        }

        private void Add(Location location, Severity severity, ReviewCategory category, string problem, string suggestion)
        {
            var line = location.GetLineSpan().StartLinePosition.Line + 1;
            _findings.Add(new ReviewFinding(severity, category, _fileName, line, problem, suggestion, FindingSource.StaticAnalysis));
        }
    }
}
