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
        Func<Task> action,
        CancellationToken cancellationToken = default)
        => Runner.AssertAsync(options, action, cancellationToken);

    /// <summary>
    /// Asserts a budget around an action that produces a value, and returns it.
    /// </summary>
    public static Task<T> AssertAsync<T>(
        QueryBudgetOptions options,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
        => Runner.AssertAsync(options, action, cancellationToken);

    public static Task<QueryBudgetMeasurement<T>> MeasureAsync<T>(
        QueryBudgetOptions options,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
        => Runner.MeasureAsync(options, action, cancellationToken);

    public static async Task<QueryBudgetMeasurement<object?>> MeasureAsync(
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return await Runner.MeasureAsync(
            new QueryBudgetOptions(),
            async () =>
            {
                await action().ConfigureAwait(false);
                return (object?)null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static Task<QueryBudgetMeasurement<T>> MeasureAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
        => Runner.MeasureAsync(new QueryBudgetOptions(), action, cancellationToken);
}
