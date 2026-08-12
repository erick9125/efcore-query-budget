namespace EfCoreQueryBudget;

/// <summary>
/// Outcome of evaluating captured metrics against a budget.
/// </summary>
public sealed record QueryBudgetResult
{
    public required bool Passed { get; init; }

    public required QueryBudgetOptions Budget { get; init; }

    public required QueryMetrics Metrics { get; init; }

    public IReadOnlyList<QueryBudgetViolation> Violations { get; init; }
        = Array.Empty<QueryBudgetViolation>();
}
