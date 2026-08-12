using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class DuplicateAndPatternDetectorTests
{
    [Fact]
    public void Detects_exact_duplicates()
    {
        var queries = new[]
        {
            Query("SELECT * FROM users WHERE id = @id", ("@id", 10)),
            Query("SELECT * FROM users WHERE id = @id", ("@id", 10)),
            Query("SELECT * FROM users WHERE id = @id", ("@id", 10))
        };

        var groups = new ExactDuplicateDetector().Detect(queries);
        groups.Should().ContainSingle();
        groups[0].ExecutionCount.Should().Be(3);
        groups[0].DistinctParameterSetCount.Should().Be(1);
    }

    [Fact]
    public void Detects_repeated_patterns_with_distinct_parameters()
    {
        var queries = Enumerable.Range(1, 6)
            .Select(i => Query("SELECT * FROM users WHERE id = @id", ("@id", i)))
            .ToArray();

        var groups = new RepeatedPatternDetector().Detect(queries, threshold: 5);
        groups.Should().ContainSingle();
        groups[0].ExecutionCount.Should().Be(6);
        groups[0].DistinctParameterSetCount.Should().Be(6);
    }

    [Fact]
    public void Exact_duplicates_are_not_repeated_patterns()
    {
        var queries = Enumerable.Range(0, 6)
            .Select(_ => Query("SELECT * FROM users WHERE id = @id", ("@id", 10)))
            .ToArray();

        new RepeatedPatternDetector().Detect(queries, threshold: 5)
            .Should().BeEmpty();
    }

    private static RecordedQuery Query(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        return new RecordedQuery
        {
            CommandText = sql,
            Parameters = parameters.ToDictionary(p => p.Name, p => p.Value),
            Duration = TimeSpan.FromMilliseconds(5),
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
