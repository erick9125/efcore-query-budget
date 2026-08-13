using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class QueryBudgetViolationTests
{
    private readonly QueryBudgetEvaluator _evaluator = new();
    private readonly QueryMetricsCalculator _calculator = new();

    [Theory]
    [InlineData(QueryBudgetViolationType.QueryCountExceeded)]
    [InlineData(QueryBudgetViolationType.ExactDuplicatesExceeded)]
    [InlineData(QueryBudgetViolationType.RepeatedPatternsExceeded)]
    [InlineData(QueryBudgetViolationType.PatternExecutionsExceeded)]
    [InlineData(QueryBudgetViolationType.SlowQueriesExceeded)]
    public void Count_limits_produce_a_count_violation(QueryBudgetViolationType type)
    {
        Violation(type).Should().BeOfType<CountBudgetViolation>();
    }

    [Theory]
    [InlineData(QueryBudgetViolationType.TotalDurationExceeded)]
    [InlineData(QueryBudgetViolationType.SingleQueryDurationExceeded)]
    public void Duration_limits_produce_a_duration_violation(QueryBudgetViolationType type)
    {
        Violation(type).Should().BeOfType<DurationBudgetViolation>();
    }

    [Fact]
    public void A_count_violation_carries_integers()
    {
        var violation = (CountBudgetViolation)Violation(
            QueryBudgetViolationType.QueryCountExceeded);

        violation.Budget.Should().Be(1);
        violation.Actual.Should().Be(6);
    }

    [Fact]
    public void A_duration_violation_carries_timespans()
    {
        var violation = (DurationBudgetViolation)Violation(
            QueryBudgetViolationType.TotalDurationExceeded);

        violation.Budget.Should().Be(TimeSpan.FromMilliseconds(10));
        violation.Actual.Should().Be(TimeSpan.FromMilliseconds(600));
    }

    [Fact]
    public void A_passing_budget_produces_no_violations()
    {
        var result = Evaluate(new QueryBudgetOptions { MaxQueries = 100 });

        result.Passed.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    private QueryBudgetViolation Violation(QueryBudgetViolationType type)
    {
        var result = Evaluate(BudgetFor(type));

        return result.Violations.Should().ContainSingle(v => v.Type == type).Subject;
    }

    private QueryBudgetResult Evaluate(QueryBudgetOptions budget)
        => _evaluator.Evaluate(_calculator.Calculate(Queries(), budget));

    /// <summary>
    /// A budget that trips exactly one limit against <see cref="Queries"/>: six executions of one
    /// read pattern, five of them identical, each taking 100 ms.
    /// </summary>
    private static QueryBudgetOptions BudgetFor(QueryBudgetViolationType type)
    {
        return type switch
        {
            QueryBudgetViolationType.QueryCountExceeded =>
                new QueryBudgetOptions { MaxQueries = 1 },
            QueryBudgetViolationType.ExactDuplicatesExceeded =>
                new QueryBudgetOptions { MaxExactDuplicates = 0 },
            QueryBudgetViolationType.RepeatedPatternsExceeded =>
                new QueryBudgetOptions { MaxRepeatedPatterns = 0, RepeatedPatternThreshold = 5 },
            QueryBudgetViolationType.PatternExecutionsExceeded =>
                new QueryBudgetOptions { MaxExecutionsPerPattern = 2, RepeatedPatternThreshold = 5 },
            QueryBudgetViolationType.SlowQueriesExceeded =>
                new QueryBudgetOptions
                {
                    MaxSlowQueries = 0,
                    SlowQueryThreshold = TimeSpan.FromMilliseconds(50)
                },
            QueryBudgetViolationType.TotalDurationExceeded =>
                new QueryBudgetOptions { MaxTotalDuration = TimeSpan.FromMilliseconds(10) },
            QueryBudgetViolationType.SingleQueryDurationExceeded =>
                new QueryBudgetOptions { MaxSingleQueryDuration = TimeSpan.FromMilliseconds(10) },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static RecordedQuery[] Queries()
    {
        // Six of the same shape so a pattern forms, with a repeat so duplicates form too.
        return Enumerable.Range(0, 6)
            .Select(i => new RecordedQuery
            {
                CommandText = "SELECT * FROM users WHERE id = @id",
                Parameters = new Dictionary<string, object?> { ["@id"] = i == 0 ? 1 : i },
                Duration = TimeSpan.FromMilliseconds(100),
                Timestamp = DateTimeOffset.UnixEpoch
            })
            .ToArray();
    }
}
