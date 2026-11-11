using AI.CodeReview.Infrastructure.Prompts;
using Xunit;

namespace AI.CodeReview.Tests.Prompts;

public class ReviewLanguageTests
{
    [Theory]
    [InlineData("src/Foo.cs", "C#")]
    [InlineData("web/app.tsx", "TypeScript")]
    [InlineData("web/app.jsx", "JavaScript")]
    [InlineData("tools/build.py", "Python")]
    [InlineData("Views/Index.cshtml", "Razor")]
    [InlineData("styles/site.scss", "SCSS")]
    public void FromFileName_KnownExtension_ReturnsLanguageName(string fileName, string expected)
        => Assert.Equal(expected, ReviewLanguage.FromFileName(fileName));

    [Fact]
    public void FromFileName_IsCaseInsensitive()
        => Assert.Equal("C#", ReviewLanguage.FromFileName("Program.CS"));

    [Theory]
    [InlineData("Makefile")]
    [InlineData("archive.zip")]
    // YAML/JSON were removed from the AI-only allowlist (see CodeReviewOrchestrator), so these
    // never reach ReviewLanguage in practice — covered here anyway to document that
    // FromFileName itself still falls back gracefully rather than erroring on them.
    [InlineData(".github/workflows/review.yml")]
    [InlineData("appsettings.json")]
    public void FromFileName_UnknownExtension_FallsBackToGenericName(string fileName)
        => Assert.Equal("software", ReviewLanguage.FromFileName(fileName));
}
