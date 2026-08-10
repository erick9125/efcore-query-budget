using ErickMorales.EntityFrameworkCore.QueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.UnitTests;

public class DefaultSqlNormalizerTests
{
    private readonly DefaultSqlNormalizer _normalizer = new();

    [Fact]
    public void Normalize_collapses_whitespace()
    {
        var result = _normalizer.Normalize("SELECT  *\nFROM   users");
        result.Should().Be("SELECT * FROM users");
    }

    [Fact]
    public void Normalize_is_stable()
    {
        const string sql = "  SELECT id FROM users WHERE id = @p  ";
        _normalizer.Normalize(sql).Should().Be(_normalizer.Normalize(sql));
    }
}
