namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// Result of measuring queries without asserting a budget.
/// </summary>
public sealed record QueryBudgetMeasurement<T>
{
    public required T Value { get; init; }

    public required QueryMetrics Metrics { get; init; }

    public required QueryBudgetResult Result { get; init; }
}
