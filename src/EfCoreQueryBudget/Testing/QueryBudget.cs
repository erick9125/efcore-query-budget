namespace EfCoreQueryBudget;

/// <summary>
/// Primary testing API for measuring and asserting EF Core query budgets.
/// </summary>
/// <remarks>
/// A shortcut over a default <see cref="QueryBudgetRunner"/>. Build a runner of your own when you
/// need to replace the normalizer, the fingerprinter or the report formatter.
/// </remarks>
public static class QueryBudget
{
    private static readonly QueryBudgetRunner Runner = new();

    public static Task AssertAsync(
        QueryBudgetOptions options,
        Func<Task> action)
        => Runner.AssertAsync(options, action);

    public static Task AssertAsync(Func<Task> action, QueryBudgetOptions options)
        => Runner.AssertAsync(options, action);

    public static Task<QueryBudgetMeasurement<T>> MeasureAsync<T>(
        QueryBudgetOptions options,
        Func<Task<T>> action)
        => Runner.MeasureAsync(options, action);

    public static async Task<QueryBudgetMeasurement<object?>> MeasureAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return await Runner.MeasureAsync(new QueryBudgetOptions(), async () =>
        {
            await action().ConfigureAwait(false);
            return (object?)null;
        }).ConfigureAwait(false);
    }

    public static Task<QueryBudgetMeasurement<T>> MeasureAsync<T>(Func<Task<T>> action)
        => Runner.MeasureAsync(new QueryBudgetOptions(), action);
}
