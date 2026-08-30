using AI.CodeReview.Infrastructure.Diffing;
using Xunit;

namespace AI.CodeReview.Tests.Diffing;

public class UnifiedDiffParserTests
{
    private readonly UnifiedDiffParser _parser = new();

    [Fact]
    public void Parse_EmptyDiff_ReturnsEmptyList()
    {
        var result = _parser.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceOnlyDiff_ReturnsEmptyList()
    {
        var result = _parser.Parse("   \n  ");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleFile_ExtractsFileNameAndAddedLinesWithCorrectLineNumbers()
    {
        var diff = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc123..def456 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -10,3 +10,5 @@ namespace Foo
             public void Bar()
             {
            +    var x = 1;
            +    var y = 2;
             }
            """;

        var result = _parser.Parse(diff);

        var file = Assert.Single(result);
        Assert.Equal("src/Foo.cs", file.FileName);
        Assert.Equal(2, file.AddedLines.Count);
        Assert.Equal(12, file.AddedLines[0].LineNumber);
        Assert.Equal("    var x = 1;", file.AddedLines[0].Content);
        Assert.Equal(13, file.AddedLines[1].LineNumber);
        Assert.Equal("    var y = 2;", file.AddedLines[1].Content);
    }

    [Fact]
    public void Parse_MultipleFiles_ReturnsAllFilesWithIndependentLineCounters()
    {
        var diff = """
            diff --git a/src/A.cs b/src/A.cs
            index 111..222 100644
            --- a/src/A.cs
            +++ b/src/A.cs
            @@ -1,2 +1,3 @@
             namespace A;
            +public class A { }
             // end
            diff --git a/src/B.cs b/src/B.cs
            index 333..444 100644
            --- a/src/B.cs
            +++ b/src/B.cs
            @@ -5,2 +5,3 @@
             namespace B;
            +public class B { }
            """;

        var result = _parser.Parse(diff);

        Assert.Equal(2, result.Count);

        var fileA = result[0];
        Assert.Equal("src/A.cs", fileA.FileName);
        var addedA = Assert.Single(fileA.AddedLines);
        Assert.Equal(2, addedA.LineNumber);
        Assert.Equal("public class A { }", addedA.Content);

        var fileB = result[1];
        Assert.Equal("src/B.cs", fileB.FileName);
        var addedB = Assert.Single(fileB.AddedLines);
        Assert.Equal(6, addedB.LineNumber);
        Assert.Equal("public class B { }", addedB.Content);
    }

    [Fact]
    public void Parse_DeletedLines_AreIgnoredAndDoNotAffectLineNumbering()
    {
        var diff = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc123..def456 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,4 +1,3 @@
             namespace Foo;
            -public class Old { }
            -// removed comment
             public class Kept { }
            """;

        var result = _parser.Parse(diff);

        var file = Assert.Single(result);
        Assert.Equal("src/Foo.cs", file.FileName);
        Assert.Empty(file.AddedLines);
    }
}
