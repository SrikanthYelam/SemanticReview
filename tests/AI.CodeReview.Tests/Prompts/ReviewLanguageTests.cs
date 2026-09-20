using AI.CodeReview.Infrastructure.Prompts;
using Xunit;

namespace AI.CodeReview.Tests.Prompts;

public class ReviewLanguageTests
{
    [Theory]
    [InlineData("src/Foo.cs", "C#")]
    [InlineData(".github/workflows/review.yml", "YAML")]
    [InlineData("config/app.yaml", "YAML")]
    [InlineData("appsettings.json", "JSON")]
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
    public void FromFileName_UnknownExtension_FallsBackToGenericName(string fileName)
        => Assert.Equal("software", ReviewLanguage.FromFileName(fileName));
}
