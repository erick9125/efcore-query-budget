using EfCoreQueryBudget;
using FluentAssertions;

namespace EfCoreQueryBudget.Tests.Unit;

public class DefaultQueryReportFormatterTests
{
    private readonly DefaultQueryReportFormatter _formatter = new();

    [Fact]
    public void A_met_budget_says_so()
    {
        var report = Format(new QueryBudgetOptions { MaxQueries = 10 }, Reads(1));

        report.Should().StartWith("EF Core query budget met");
        report.Should().NotContain("exceeded");
    }

    [Fact]
    public void The_scope_label_is_shown_when_set()
    {
        Format(new QueryBudgetOptions { ScopeLabel = "GET /api/orders" }, Reads(1))
            .Should().Contain("Scope: GET /api/orders");
    }

    [Fact]
    public void A_report_without_a_scope_label_does_not_show_an_empty_one()
    {
        Format(new QueryBudgetOptions(), Reads(1)).Should().NotContain("Scope:");
    }

    [Fact]
    public void A_count_violation_shows_the_budget_and_the_actual_value()
    {
        var report = Format(new QueryBudgetOptions { MaxQueries = 2 }, Reads(5));

        report.Should().Contain("Query count");
        report.Should().Contain("Budget: <= 2");
        report.Should().Contain("Actual:   5");
    }

    [Theory]
    [InlineData("Query count")]
    [InlineData("Exact duplicates")]
    [InlineData("Repeated query patterns")]
    [InlineData("Executions in a single pattern")]
    [InlineData("Slow queries")]
    [InlineData("Total database time")]
    [InlineData("Single query duration")]
    public void Every_limit_keeps_its_label(string label)
    {
        // The labels moved out of the evaluator and into this formatter; the report must read
        // exactly as it did before.
        var budget = new QueryBudgetOptions
        {
            MaxQueries = 1,
            MaxExactDuplicates = 0,
            MaxRepeatedPatterns = 0,
            MaxExecutionsPerPattern = 2,
            MaxSlowQueries = 0,
            MaxTotalDuration = TimeSpan.FromMilliseconds(10),
            MaxSingleQueryDuration = TimeSpan.FromMilliseconds(10),
            SlowQueryThreshold = TimeSpan.FromMilliseconds(50),
            RepeatedPatternThreshold = 5
        };

        var queries = Enumerable.Range(0, 6)
            .Select(i => new RecordedQuery
            {
                CommandText = "SELECT * FROM users WHERE id = @id",
                Parameters = new Dictionary<string, object?> { ["@id"] = i == 0 ? 1 : i },
                Duration = TimeSpan.FromMilliseconds(100),
                Timestamp = DateTimeOffset.UnixEpoch
            })
            .ToArray();

        Format(budget, queries).Should().Contain(label);
    }

    [Fact]
    public void A_duration_violation_is_shown_in_milliseconds()
    {
        var budget = new QueryBudgetOptions
        {
            MaxTotalDuration = TimeSpan.FromMilliseconds(150)
        };

        var queries = Enumerable.Range(0, 2)
            .Select(i => Query($"SELECT {i}", TimeSpan.FromMilliseconds(100)))
            .ToArray();

        var report = Format(budget, queries);

        report.Should().Contain("Total database time");
        report.Should().Contain("Budget: <= 150 ms");
        report.Should().Contain("Actual:   200 ms");
    }

    [Fact]
    public void Only_three_groups_are_shown_and_the_rest_are_counted()
    {
        // Five distinct read patterns, each above the repeat threshold.
        var queries = Enumerable.Range(0, 5)
            .SelectMany(table => Enumerable.Range(0, 6)
                .Select(i => Query($"SELECT * FROM t{table} WHERE id = @p", parameter: i)))
            .ToArray();

        var report = Format(new QueryBudgetOptions { MaxQueries = 0 }, queries);

        report.Should().Contain("... and 2 more groups");
    }

    [Fact]
    public void A_single_hidden_group_is_counted_in_the_singular()
    {
        var queries = Enumerable.Range(0, 4)
            .SelectMany(table => Enumerable.Range(0, 6)
                .Select(i => Query($"SELECT * FROM t{table} WHERE id = @p", parameter: i)))
            .ToArray();

        var report = Format(new QueryBudgetOptions { MaxQueries = 0 }, queries);

        report.Should().Contain("... and 1 more group");
        report.Should().NotContain("more groups");
    }

    [Fact]
    public void Hidden_is_the_default_and_explains_itself()
    {
        var report = Format(new QueryBudgetOptions { MaxQueries = 0 }, Reads(2));

        report.Should().Contain("Parameter values are hidden");
    }

    [Fact]
    public void Types_only_shows_names_and_types_but_not_values()
    {
        var report = Format(
            new QueryBudgetOptions
            {
                MaxQueries = 0,
                ParameterDisplayMode = QueryParameterDisplayMode.TypesOnly
            },
            Duplicated("@id", 42));

        report.Should().Contain("@id: Int32");
        report.Should().NotContain("42");
        report.Should().NotContain("Parameter values are hidden");
    }

    [Fact]
    public void Full_shows_the_values()
    {
        var report = Format(
            new QueryBudgetOptions
            {
                MaxQueries = 0,
                ParameterDisplayMode = QueryParameterDisplayMode.Full
            },
            Duplicated("@id", 42));

        report.Should().Contain("@id=42");
    }

    [Fact]
    public void A_string_value_is_quoted()
    {
        var report = Format(
            new QueryBudgetOptions
            {
                MaxQueries = 0,
                ParameterDisplayMode = QueryParameterDisplayMode.Full
            },
            Duplicated("@name", "ana"));

        report.Should().Contain("@name=\"ana\"");
    }

    [Fact]
    public void A_command_without_parameters_says_none()
    {
        var queries = Enumerable.Range(0, 2)
            .Select(_ => Query("SELECT 1"))
            .ToArray();

        var report = Format(
            new QueryBudgetOptions
            {
                MaxQueries = 0,
                ParameterDisplayMode = QueryParameterDisplayMode.Full
            },
            queries);

        report.Should().Contain("Sample parameters: (none)");
    }

    private string Format(QueryBudgetOptions budget, RecordedQuery[] queries)
    {
        var metrics = new QueryMetricsCalculator().Calculate(queries, budget);
        return _formatter.Format(new QueryBudgetEvaluator().Evaluate(metrics));
    }

    private static RecordedQuery[] Reads(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => Query($"SELECT {i}"))
            .ToArray();
    }

    private static RecordedQuery[] Duplicated(string name, object value)
    {
        return Enumerable.Range(0, 2)
            .Select(_ => new RecordedQuery
            {
                CommandText = "SELECT * FROM users WHERE id = @id",
                Parameters = new Dictionary<string, object?> { [name] = value },
                Duration = TimeSpan.FromMilliseconds(1),
                Timestamp = DateTimeOffset.UnixEpoch
            })
            .ToArray();
    }

    private static RecordedQuery Query(
        string sql,
        TimeSpan? duration = null,
        int? parameter = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (parameter is not null)
        {
            parameters["@p"] = parameter;
        }

        return new RecordedQuery
        {
            CommandText = sql,
            Parameters = parameters,
            Duration = duration ?? TimeSpan.FromMilliseconds(1),
            Timestamp = DateTimeOffset.UnixEpoch
        };
    }
}
