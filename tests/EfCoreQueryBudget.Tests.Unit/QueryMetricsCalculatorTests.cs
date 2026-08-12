using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class QueryMetricsCalculatorTests
{
    private static readonly QueryBudgetOptions Masking = new()
    {
        SqlNormalization = SqlNormalizationMode.MaskLiterals
    };

    [Fact]
    public void The_normalization_option_reaches_the_pattern_detector()
    {
        var queries = InlineLiteralNPlusOne();
        var calculator = new QueryMetricsCalculator();

        calculator.Calculate(queries).RepeatedPatternCount.Should().Be(0);
        calculator.Calculate(queries, Masking).RepeatedPatternCount.Should().Be(1);
    }

    [Fact]
    public void Masking_does_not_turn_different_literals_into_exact_duplicates()
    {
        var queries = InlineLiteralNPlusOne();

        // Exact duplicates are computed off an unmasked fingerprint, so six different ids stay
        // six different queries however the pattern side normalizes.
        var metrics = new QueryMetricsCalculator().Calculate(queries, Masking);

        metrics.ExactDuplicateCount.Should().Be(0);
        metrics.ExactDuplicateGroups.Should().BeEmpty();
    }

    [Fact]
    public void An_injected_detector_wins_over_the_option()
    {
        var calculator = new QueryMetricsCalculator(
            repeatedPatternDetector: new RepeatedPatternDetector());

        calculator.Calculate(InlineLiteralNPlusOne(), Masking)
            .RepeatedPatternCount.Should().Be(0);
    }

    [Fact]
    public void Without_totals_the_aggregates_come_from_the_queries()
    {
        var metrics = new QueryMetricsCalculator().Calculate(InlineLiteralNPlusOne());

        metrics.QueryCount.Should().Be(6);
        metrics.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(30));
        metrics.DiscardedQueryCount.Should().Be(0);
    }

    [Fact]
    public void With_totals_the_aggregates_come_from_the_scope()
    {
        var totals = new QueryCaptureTotals
        {
            ExecutionCount = 900,
            DiscardedQueryCount = 894,
            TotalDuration = TimeSpan.FromSeconds(3),
            MaximumDuration = TimeSpan.FromMilliseconds(400),
            SlowQueryCount = 12
        };

        var metrics = new QueryMetricsCalculator()
            .Calculate(InlineLiteralNPlusOne(), new QueryBudgetOptions(), totals);

        metrics.QueryCount.Should().Be(900);
        metrics.TotalDuration.Should().Be(TimeSpan.FromSeconds(3));
        metrics.MaximumDuration.Should().Be(TimeSpan.FromMilliseconds(400));
        metrics.SlowQueryCount.Should().Be(12);
        metrics.DiscardedQueryCount.Should().Be(894);

        // The groups still describe what was retained.
        metrics.Queries.Should().HaveCount(6);
    }

    private static RecordedQuery[] InlineLiteralNPlusOne()
    {
        return Enumerable.Range(1, 6)
            .Select(i => new RecordedQuery
            {
                CommandText = $"SELECT * FROM users WHERE id = {i}",
                Parameters = new Dictionary<string, object?>(),
                Duration = TimeSpan.FromMilliseconds(5),
                Timestamp = DateTimeOffset.UtcNow
            })
            .ToArray();
    }
}
