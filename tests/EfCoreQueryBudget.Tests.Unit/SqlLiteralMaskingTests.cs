using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class SqlLiteralMaskingTests
{
    private readonly DefaultSqlNormalizer _masking = new(SqlNormalizationMode.MaskLiterals);

    [Theory]
    [InlineData("SELECT * FROM users WHERE name = 'ana'", "SELECT * FROM users WHERE name = ?")]
    [InlineData("SELECT * FROM users WHERE name = 'O''Brien'", "SELECT * FROM users WHERE name = ?")]
    [InlineData("SELECT * FROM t WHERE a = N'x'", "SELECT * FROM t WHERE a = ?")]
    [InlineData(@"SELECT * FROM t WHERE a = E'it\'s'", "SELECT * FROM t WHERE a = ?")]
    [InlineData("SELECT * FROM t WHERE a = $$body$$", "SELECT * FROM t WHERE a = ?")]
    [InlineData("SELECT * FROM t WHERE a = $tag$body$tag$", "SELECT * FROM t WHERE a = ?")]
    public void Masks_string_literals(string sql, string expected)
    {
        _masking.Normalize(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE id = 10", "SELECT * FROM t WHERE id = ?")]
    [InlineData("SELECT * FROM t WHERE rate = 1.5", "SELECT * FROM t WHERE rate = ?")]
    [InlineData("SELECT * FROM t WHERE rate = .5", "SELECT * FROM t WHERE rate = ?")]
    [InlineData("SELECT * FROM t WHERE rate = 1e-5", "SELECT * FROM t WHERE rate = ?")]
    [InlineData("SELECT * FROM t WHERE mask = 0x1F", "SELECT * FROM t WHERE mask = ?")]
    public void Masks_numeric_literals(string sql, string expected)
    {
        _masking.Normalize(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT * FROM table1 WHERE col_2 = 1", "SELECT * FROM table1 WHERE col_2 = ?")]
    [InlineData("SELECT t0.Id FROM users AS t0", "SELECT t0.Id FROM users AS t0")]
    [InlineData("SELECT * FROM #temp1", "SELECT * FROM #temp1")]
    public void Leaves_digits_inside_identifiers_alone(string sql, string expected)
    {
        _masking.Normalize(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT * FROM \"Order 1\" WHERE x = 1", "SELECT * FROM \"Order 1\" WHERE x = ?")]
    [InlineData("SELECT * FROM [Order 1] WHERE x = 1", "SELECT * FROM [Order 1] WHERE x = ?")]
    [InlineData("SELECT * FROM `order 1` WHERE x = 1", "SELECT * FROM `order 1` WHERE x = ?")]
    public void Leaves_quoted_identifiers_alone(string sql, string expected)
    {
        _masking.Normalize(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE id = @p0", "SELECT * FROM t WHERE id = @p0")]
    [InlineData("SELECT * FROM t WHERE id = @__id_0", "SELECT * FROM t WHERE id = @__id_0")]
    [InlineData("SELECT * FROM t WHERE id = :id", "SELECT * FROM t WHERE id = :id")]
    [InlineData("SELECT * FROM t WHERE id = $1", "SELECT * FROM t WHERE id = $1")]
    [InlineData("SELECT @@ROWCOUNT", "SELECT @@ROWCOUNT")]
    public void Leaves_parameters_alone(string sql, string expected)
    {
        _masking.Normalize(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE x IS NULL", "SELECT * FROM t WHERE x IS NULL")]
    [InlineData("SELECT * FROM t WHERE x IS NOT NULL", "SELECT * FROM t WHERE x IS NOT NULL")]
    [InlineData("SELECT * FROM t WHERE x = TRUE", "SELECT * FROM t WHERE x = TRUE")]
    public void Leaves_null_and_boolean_keywords_alone(string sql, string expected)
    {
        _masking.Normalize(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE id IN (1, 2, 3)", "SELECT * FROM t WHERE id IN (?)")]
    [InlineData("SELECT * FROM t WHERE id IN (@p0, @p1)", "SELECT * FROM t WHERE id IN (?)")]
    [InlineData("SELECT * FROM t WHERE id in (1,2)", "SELECT * FROM t WHERE id in (?)")]
    [InlineData("SELECT * FROM t WHERE id IN (1)", "SELECT * FROM t WHERE id IN (?)")]
    public void Collapses_in_lists(string sql, string expected)
    {
        _masking.Normalize(sql).Should().Be(expected);
    }

    [Fact]
    public void Does_not_collapse_value_lists()
    {
        // A different column count is a genuinely different query shape.
        _masking.Normalize("INSERT INTO t (a, b) VALUES (1, 2)")
            .Should().Be("INSERT INTO t (a, b) VALUES (?, ?)");
    }

    [Fact]
    public void Does_not_collapse_a_word_ending_in_in()
    {
        _masking.Normalize("SELECT MIN (1, 2)").Should().Be("SELECT MIN (?, ?)");
    }

    [Fact]
    public void Keeps_comments_and_does_not_desynchronize_on_a_quote_inside_one()
    {
        _masking.Normalize("-- it's a tag\nSELECT * FROM t WHERE a = 'x'")
            .Should().Be("-- it's a tag\nSELECT * FROM t WHERE a = ?");

        _masking.Normalize("/* it's a tag */ SELECT * FROM t WHERE a = 'x'")
            .Should().Be("/* it's a tag */ SELECT * FROM t WHERE a = ?");
    }

    [Fact]
    public void Keeps_the_newline_that_terminates_a_line_comment()
    {
        // Collapsing it into a space would swallow the statement into the comment.
        _masking.Normalize("-- tag\n\n  SELECT 1")
            .Should().Be("-- tag\nSELECT ?");
    }

    [Fact]
    public void Collapses_whitespace()
    {
        _masking.Normalize("SELECT  *\nFROM   users").Should().Be("SELECT * FROM users");
    }

    [Theory]
    [InlineData("SELECT * FROM t WHERE a = 'x' AND b IN (1, 2) -- tag")]
    [InlineData("SELECT * FROM \"Order\" WHERE id = @p0")]
    [InlineData("SELECT * FROM t WHERE a = 'unterminated")]
    public void Is_idempotent(string sql)
    {
        var once = _masking.Normalize(sql);
        _masking.Normalize(once).Should().Be(once);
    }

    [Fact]
    public void Whitespace_only_mode_leaves_literals_untouched()
    {
        var whitespaceOnly = new DefaultSqlNormalizer();

        whitespaceOnly.Normalize("SELECT  * FROM t WHERE id = 10 AND a = 'x'")
            .Should().Be("SELECT * FROM t WHERE id = 10 AND a = 'x'");
    }
}
