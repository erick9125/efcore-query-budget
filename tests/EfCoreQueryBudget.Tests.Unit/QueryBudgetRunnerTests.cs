using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

/// <summary>
/// The runner is the composition root, so these tests are about the three abstractions being
/// reachable — before it existed, an <see cref="ISqlNormalizer"/> or an
/// <see cref="IQueryReportFormatter"/> of your own had nowhere to go.
/// </summary>
public class QueryBudgetRunnerTests
{
    [Fact]
    public async Task A_custom_normalizer_reaches_the_grouping()
    {
        // Folds every SELECT into one shape, so six different tables become one repeated pattern.
        var runner = new QueryBudgetRunner(
            new StubAnalysisFactory(new CollapsingNormalizer()),
            new DefaultQueryReportFormatter());

        var measurement = await runner.MeasureAsync(
            new QueryBudgetOptions { RepeatedPatternThreshold = 5 },
            () =>
            {
                for (var i = 0; i < 6; i++)
                {
                    Record($"SELECT * FROM t{i} WHERE id = {i}");
                }

                return Task.FromResult(0);
            });

        measurement.Metrics.RepeatedPatternCount.Should().Be(1);
        measurement.Metrics.RepeatedPatternGroups[0].ExecutionCount.Should().Be(6);
    }

    [Fact]
    public async Task The_default_runner_leaves_those_six_queries_alone()
    {
        // Same input through the default pipeline: six shapes, no pattern. Isolates the normalizer
        // as the only difference.
        var measurement = await new QueryBudgetRunner().MeasureAsync(
            new QueryBudgetOptions { RepeatedPatternThreshold = 5 },
            () =>
            {
                for (var i = 0; i < 6; i++)
                {
                    Record($"SELECT * FROM t{i} WHERE id = {i}");
                }

                return Task.FromResult(0);
            });

        measurement.Metrics.RepeatedPatternCount.Should().Be(0);
    }

    [Fact]
    public async Task A_custom_formatter_writes_the_exception_message()
    {
        var runner = new QueryBudgetRunner(
            new DefaultQueryAnalysisFactory(),
            new StubReportFormatter());

        var act = async () => await runner.AssertAsync(
            new QueryBudgetOptions { MaxQueries = 0 },
            () =>
            {
                Record("SELECT 1");
                return Task.CompletedTask;
            });

        var exception = await act.Should().ThrowAsync<QueryBudgetExceededException>();
        exception.Which.Message.Should().Be("over budget: 1 query(s)");
    }

    [Fact]
    public async Task The_default_runner_agrees_with_the_static_facade()
    {
        var throughFacade = await QueryBudget.MeasureAsync(() =>
        {
            Record("SELECT 1");
            Record("SELECT 1");
            return Task.CompletedTask;
        });

        var throughRunner = await new QueryBudgetRunner().MeasureAsync(
            new QueryBudgetOptions(),
            () =>
            {
                Record("SELECT 1");
                Record("SELECT 1");
                return Task.FromResult(0);
            });

        throughRunner.Metrics.QueryCount.Should().Be(throughFacade.Metrics.QueryCount);
        throughRunner.Metrics.RedundantExecutionCount
            .Should().Be(throughFacade.Metrics.RedundantExecutionCount);
    }

    [Fact]
    public async Task The_result_carries_the_budget_it_was_measured_against()
    {
        var budget = new QueryBudgetOptions { MaxQueries = 3, ScopeLabel = "runner" };

        var measurement = await new QueryBudgetRunner()
            .MeasureAsync(budget, () => Task.FromResult(0));

        measurement.Result.Budget.Should().BeSameAs(budget);
        measurement.Metrics.Budget.Should().BeSameAs(budget);
    }

    private static void Record(string sql)
    {
        QueryBudgetContext.Record(new RecordedQuery
        {
            CommandText = sql,
            Duration = TimeSpan.FromMilliseconds(1),
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    private sealed class StubAnalysisFactory : IQueryAnalysisFactory
    {
        private readonly ISqlNormalizer _normalizer;

        public StubAnalysisFactory(ISqlNormalizer normalizer)
        {
            _normalizer = normalizer;
        }

        public ISqlNormalizer CreateNormalizer(SqlNormalizationMode mode) => _normalizer;

        public IQueryFingerprinter CreateFingerprinter(SqlNormalizationMode mode)
        {
            // Structural through the custom normalizer, exact through the default one, so the
            // variants that a pattern group needs are still told apart.
            return new DefaultQueryFingerprinter(
                _normalizer,
                new DefaultSqlNormalizer(SqlNormalizationMode.WhitespaceOnly));
        }
    }

    /// <summary>Folds every read into one shape, which no built-in normalizer does.</summary>
    private sealed class CollapsingNormalizer : ISqlNormalizer
    {
        public string Normalize(string sql)
            => sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ? "SELECT ?" : sql;
    }

    private sealed class StubReportFormatter : IQueryReportFormatter
    {
        public string Format(QueryBudgetResult result)
            => $"over budget: {result.Metrics.QueryCount} query(s)";
    }
}
