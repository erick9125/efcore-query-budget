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

        var groups = Exact().Detect(queries);
        groups.Should().ContainSingle();
        groups[0].ExecutionCount.Should().Be(3);
        groups[0].DistinctVariantCount.Should().Be(1);
    }

    [Fact]
    public void Detects_repeated_patterns_with_distinct_parameters()
    {
        var queries = Enumerable.Range(1, 6)
            .Select(i => Query("SELECT * FROM users WHERE id = @id", ("@id", i)))
            .ToArray();

        var groups = Patterns().Detect(queries, threshold: 5);
        groups.Should().ContainSingle();
        groups[0].ExecutionCount.Should().Be(6);
        groups[0].DistinctVariantCount.Should().Be(6);
    }

    [Fact]
    public void Exact_duplicates_are_not_repeated_patterns()
    {
        var queries = Enumerable.Range(0, 6)
            .Select(_ => Query("SELECT * FROM users WHERE id = @id", ("@id", 10)))
            .ToArray();

        Patterns().Detect(queries, threshold: 5)
            .Should().BeEmpty();
    }

    [Fact]
    public void Inline_literals_hide_a_repeated_pattern_from_the_default_normalizer()
    {
        Patterns().Detect(InlineLiteralNPlusOne(), threshold: 5)
            .Should().BeEmpty();
    }

    [Fact]
    public void Masking_literals_reveals_a_repeated_pattern_in_raw_sql()
    {
        var groups = Masking().Detect(InlineLiteralNPlusOne(), threshold: 5);

        groups.Should().ContainSingle();
        groups[0].ExecutionCount.Should().Be(6);
        groups[0].DistinctVariantCount.Should().Be(6);
        groups[0].NormalizedSql.Should().Be("SELECT * FROM users WHERE id = ?");
    }

    [Fact]
    public void Masking_literals_groups_in_lists_of_different_lengths()
    {
        var queries = Enumerable.Range(2, 6)
            .Select(i => Query($"SELECT * FROM users WHERE id IN ({string.Join(", ", Enumerable.Range(1, i))})"))
            .ToArray();

        var groups = Masking().Detect(queries, threshold: 5);

        groups.Should().ContainSingle();
        groups[0].ExecutionCount.Should().Be(6);
        groups[0].NormalizedSql.Should().Be("SELECT * FROM users WHERE id IN (?)");
    }

    [Fact]
    public void The_same_raw_query_repeated_is_not_a_pattern_even_when_masking()
    {
        var queries = Enumerable.Range(0, 6)
            .Select(_ => Query("SELECT * FROM users WHERE id = 7"))
            .ToArray();

        // One variant, so it is a redundant repeat rather than an N+1.
        Masking().Detect(queries, threshold: 5).Should().BeEmpty();
        Exact().Detect(queries).Should().ContainSingle();
    }

    private static readonly IQueryAnalysisFactory Analysis = new DefaultQueryAnalysisFactory();

    private static ExactDuplicateDetector Exact() => Detector(SqlNormalizationMode.WhitespaceOnly);

    private static RepeatedPatternDetector Patterns()
        => PatternDetector(SqlNormalizationMode.WhitespaceOnly);

    private static RepeatedPatternDetector Masking()
        => PatternDetector(SqlNormalizationMode.MaskLiterals);

    private static ExactDuplicateDetector Detector(SqlNormalizationMode mode)
    {
        return new ExactDuplicateDetector(
            Analysis.CreateNormalizer(SqlNormalizationMode.WhitespaceOnly),
            Analysis.CreateFingerprinter(mode));
    }

    private static RepeatedPatternDetector PatternDetector(SqlNormalizationMode mode)
    {
        return new RepeatedPatternDetector(
            Analysis.CreateNormalizer(mode),
            Analysis.CreateFingerprinter(mode));
    }

    private static RecordedQuery[] InlineLiteralNPlusOne()
    {
        return Enumerable.Range(1, 6)
            .Select(i => Query($"SELECT * FROM users WHERE id = {i}"))
            .ToArray();
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
