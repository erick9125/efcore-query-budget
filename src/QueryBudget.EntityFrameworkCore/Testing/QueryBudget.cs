namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// Primary testing API for measuring and asserting EF Core query budgets.
/// </summary>
public static class QueryBudget
{
    private static readonly QueryMetricsCalculator MetricsCalculator = new();
    private static readonly QueryBudgetEvaluator Evaluator = new();
    private static readonly IQueryReportFormatter ReportFormatter = new DefaultQueryReportFormatter();

    public static async Task AssertAsync(
        QueryBudgetOptions options,
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(action);

        var measurement = await MeasureAsync(options, async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

        if (!measurement.Result.Passed)
        {
            throw new QueryBudgetExceededException(
                measurement.Result,
                ReportFormatter.Format(measurement.Result));
        }
    }

    public static Task AssertAsync(Func<Task> action, QueryBudgetOptions options)
        => AssertAsync(options, action);

    public static async Task<QueryBudgetMeasurement<T>> MeasureAsync<T>(
        QueryBudgetOptions options,
        Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(action);

        using var _ = QueryBudgetContext.Begin();
        var scope = QueryBudgetContext.Current
            ?? throw new InvalidOperationException("Query budget scope was not established.");

        T value = await action().ConfigureAwait(false);

        var metrics = MetricsCalculator.Calculate(scope.Snapshot(), options);
        var result = Evaluator.Evaluate(options, metrics);

        return new QueryBudgetMeasurement<T>
        {
            Value = value,
            Metrics = metrics,
            Result = result
        };
    }

    public static async Task<QueryBudgetMeasurement<object?>> MeasureAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return await MeasureAsync(new QueryBudgetOptions(), async () =>
        {
            await action().ConfigureAwait(false);
            return (object?)null;
        }).ConfigureAwait(false);
    }

    public static async Task<QueryBudgetMeasurement<T>> MeasureAsync<T>(Func<Task<T>> action)
    {
        return await MeasureAsync(new QueryBudgetOptions(), action).ConfigureAwait(false);
    }
}
