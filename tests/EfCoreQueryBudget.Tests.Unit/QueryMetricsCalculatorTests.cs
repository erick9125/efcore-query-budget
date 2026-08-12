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
