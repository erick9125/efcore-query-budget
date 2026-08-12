namespace EfCoreQueryBudget;

/// <summary>
/// A single budget limit that was exceeded.
/// </summary>
public sealed record QueryBudgetViolation(
    QueryBudgetViolationType Type,
    object Budget,
    object Actual,
    string Label);
