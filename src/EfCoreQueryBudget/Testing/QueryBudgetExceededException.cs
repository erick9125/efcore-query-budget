using System.Diagnostics.CodeAnalysis;

namespace EfCoreQueryBudget;

/// <summary>
/// Thrown when a measured scope exceeds its budget. The message is the rendered report.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "The exception exists to carry a QueryBudgetResult; there is no meaningful "
        + "instance without one. Adding the message-only constructors would make Result nullable "
        + "and push a null check onto every caller reading the metrics off a caught exception, "
        + "which is the whole reason the type exists.")]
public sealed class QueryBudgetExceededException : Exception
{
    public QueryBudgetExceededException(QueryBudgetResult result, string message)
        : base(message)
    {
        Result = result;
    }

    /// <summary>
    /// What was measured and which limits were exceeded.
    /// </summary>
    public QueryBudgetResult Result { get; }
}
