namespace EfCoreQueryBudget;

/// <summary>
/// A single budget limit that was exceeded.
/// </summary>
/// <remarks>
/// Every limit measures either a count or a duration, so the value carries its own type instead of
/// being boxed into an <see cref="object"/>. The hierarchy is closed — the constructor is
/// <c>private protected</c> — because the report relies on being able to match it exhaustively.
/// How a violation reads is the report's business: labels live in
/// <see cref="IQueryReportFormatter"/>, not here.
/// </remarks>
public abstract record QueryBudgetViolation
{
    private protected QueryBudgetViolation(QueryBudgetViolationType type)
    {
        Type = type;
    }

    public QueryBudgetViolationType Type { get; }
}

/// <summary>
/// A limit on how many of something the scope may have.
/// </summary>
public sealed record CountBudgetViolation : QueryBudgetViolation
{
    public CountBudgetViolation(QueryBudgetViolationType type, int budget, int actual)
        : base(type)
    {
        Budget = budget;
        Actual = actual;
    }

    public int Budget { get; }

    public int Actual { get; }
}

/// <summary>
/// A limit on how long the scope may spend in the database.
/// </summary>
public sealed record DurationBudgetViolation : QueryBudgetViolation
{
    public DurationBudgetViolation(
        QueryBudgetViolationType type,
        TimeSpan budget,
        TimeSpan actual)
        : base(type)
    {
        Budget = budget;
        Actual = actual;
    }

    public TimeSpan Budget { get; }

    public TimeSpan Actual { get; }
}
