namespace EfCoreQueryBudget;

/// <summary>
/// What a captured command does, which decides whether repeating it can be called redundant.
/// </summary>
public enum QueryOperation
{
    /// <summary>
    /// A read. Running the same one twice with the same parameters returns the same rows, so the
    /// second execution is work the caller could have avoided. Only reads are counted against
    /// duplicate and pattern budgets.
    /// </summary>
    Read = 0,

    /// <summary>
    /// A write. Repeating one is not redundant by itself — two identical inserts add two rows, and
    /// an increment applied twice counts twice — so writes stay out of the budget and are reported
    /// on their own.
    /// </summary>
    Write = 1,

    /// <summary>
    /// Anything else: session settings, transaction control, DDL, sequence and function calls.
    /// Treated like a write, since it is rarely work the code under measurement chose to repeat.
    /// </summary>
    Other = 2
}
