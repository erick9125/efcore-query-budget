namespace EfCoreQueryBudget;

/// <summary>
/// Measures and asserts query budgets. This is the composition root: it holds the analysis
/// pipeline, the evaluator and the report formatter, and is the way to replace any of them.
/// </summary>
/// <remarks>
/// <see cref="QueryBudget"/> is a shortcut over a default instance of this class. Use a runner
/// directly when you want your own <see cref="ISqlNormalizer"/>, <see cref="IQueryFingerprinter"/>
/// — through an <see cref="IQueryAnalysisFactory"/> — or <see cref="IQueryReportFormatter"/>.
/// </remarks>
public sealed class QueryBudgetRunner
{
    private readonly QueryMetricsCalculator _calculator;
    private readonly QueryBudgetEvaluator _evaluator;
    private readonly IQueryReportFormatter _reportFormatter;

    public QueryBudgetRunner()
        : this(new DefaultQueryAnalysisFactory(), new DefaultQueryReportFormatter())
    {
    }

    public QueryBudgetRunner(
        IQueryAnalysisFactory analysisFactory,
        IQueryReportFormatter reportFormatter)
    {
        ArgumentNullException.ThrowIfNull(analysisFactory);
        ArgumentNullException.ThrowIfNull(reportFormatter);

        _calculator = new QueryMetricsCalculator(analysisFactory);
        _evaluator = new QueryBudgetEvaluator();
        _reportFormatter = reportFormatter;
    }

    /// <summary>
    /// Runs <paramref name="action"/> inside a measurement scope and throws
    /// <see cref="QueryBudgetExceededException"/> when the budget is exceeded.
    /// </summary>
    public async Task AssertAsync(
        QueryBudgetOptions options,
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await AssertAsync(
            options,
            async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <paramref name="action"/> inside a measurement scope, throws
    /// <see cref="QueryBudgetExceededException"/> when the budget is exceeded, and otherwise
    /// returns what the action produced.
    /// </summary>
    public async Task<T> AssertAsync<T>(
        QueryBudgetOptions options,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var measurement = await MeasureAsync(options, action, cancellationToken)
            .ConfigureAwait(false);

        if (!measurement.Result.Passed)
        {
            throw new QueryBudgetExceededException(
                measurement.Result,
                _reportFormatter.Format(measurement.Result));
        }

        return measurement.Value;
    }

    /// <summary>
    /// Runs <paramref name="action"/> inside a measurement scope and returns what was measured,
    /// without throwing.
    /// </summary>
    public async Task<QueryBudgetMeasurement<T>> MeasureAsync<T>(
        QueryBudgetOptions options,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        using var scopeHandle = QueryBudgetContext.Begin(options);
        var scope = QueryBudgetContext.Current
            ?? throw new InvalidOperationException("Query budget scope was not established.");

        T value = await action().ConfigureAwait(false);

        var metrics = _calculator.Calculate(scope.Snapshot(), options, scope.Totals) with
        {
            DuplicateCaptureCount = scope.DuplicateCaptureCount
        };

        return new QueryBudgetMeasurement<T>
        {
            Value = value,
            Metrics = metrics,
            Result = _evaluator.Evaluate(metrics)
        };
    }

    /// <summary>
    /// Renders a result through this runner's report formatter.
    /// </summary>
    public string Format(QueryBudgetResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return _reportFormatter.Format(result);
    }
}
