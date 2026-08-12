namespace EfCoreQueryBudget;

/// <summary>
/// Outcome of evaluating captured metrics against a budget.
/// </summary>
public sealed record QueryBudgetResult
{
    public required bool Passed { get; init; }

    public required QueryMetrics Metrics { get; init; }

    /// <summary>
    /// The budget that was evaluated. Read from the metrics rather than stored, so the two cannot
    /// disagree about which budget produced the result.
    /// </summary>
    public QueryBudgetOptions Budget => Metrics.Budget;

    public IReadOnlyList<QueryBudgetViolation> Violations { get; init; }
        = Array.Empty<QueryBudgetViolation>();
}
