using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class SqlOperationClassifierTests
{
    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("select * from users")]
    [InlineData("  \n  SELECT 1")]
    [InlineData("(SELECT 1)")]
    public void Reads_are_reads(string sql)
    {
        SqlOperationClassifier.Classify(sql).Should().Be(QueryOperation.Read);
    }

    [Theory]
    [InlineData("INSERT INTO users (name) VALUES (@p)")]
    [InlineData("insert into users (name) values (@p)")]
    [InlineData("UPDATE users SET name = @p WHERE id = @id")]
    [InlineData("DELETE FROM users WHERE id = @id")]
    [InlineData("MERGE INTO users AS t USING s ON t.id = s.id")]
    [InlineData("TRUNCATE TABLE users")]
    public void Writes_are_writes(string sql)
    {
        SqlOperationClassifier.Classify(sql).Should().Be(QueryOperation.Write);
    }

    [Theory]
    [InlineData("SET search_path = public")]
    [InlineData("BEGIN")]
    [InlineData("COMMIT")]
    [InlineData("CREATE TABLE users (id int)")]
    [InlineData("EXEC sp_something")]
    [InlineData("")]
    [InlineData("   ")]
    public void Everything_else_is_other(string sql)
    {
        SqlOperationClassifier.Classify(sql).Should().Be(QueryOperation.Other);
    }

    [Fact]
    public void A_query_tag_does_not_hide_the_keyword()
    {
        // EF Core writes TagWith as a leading -- comment.
        SqlOperationClassifier.Classify("-- GetUsers\n\nSELECT * FROM users")
            .Should().Be(QueryOperation.Read);

        SqlOperationClassifier.Classify("-- SeedUsers\nINSERT INTO users (name) VALUES (@p)")
            .Should().Be(QueryOperation.Write);
    }

    [Fact]
    public void A_block_comment_does_not_hide_the_keyword()
    {
        SqlOperationClassifier.Classify("/* tag */ SELECT * FROM users")
            .Should().Be(QueryOperation.Read);
    }

    [Fact]
    public void A_reading_cte_is_a_read()
    {
        SqlOperationClassifier.Classify(
                "WITH recent AS (SELECT * FROM posts) SELECT * FROM recent")
            .Should().Be(QueryOperation.Read);
    }

    [Fact]
    public void A_cte_that_ends_in_a_write_is_a_write()
    {
        SqlOperationClassifier.Classify(
                "WITH stale AS (SELECT id FROM posts) DELETE FROM posts WHERE id IN (SELECT id FROM stale)")
            .Should().Be(QueryOperation.Write);
    }
}
