namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// Decides which measurement scope a captured command is attributed to.
/// </summary>
public enum ScopeAttributionMode
{
    /// <summary>
    /// Attribute a command only to the scope on its own async execution flow, and ignore commands
    /// that execute outside any scope. This is the only mode that stays correct when several
    /// scopes — or scoped and unscoped work — run concurrently in one process.
    /// </summary>
    AsyncLocalOnly = 0,

    /// <summary>
    /// Behaves like <see cref="AsyncLocalOnly"/>, except that when the execution flow carries no
    /// scope and exactly one scope is active process-wide, the command is attributed to that sole
    /// scope.
    /// </summary>
    /// <remarks>
    /// Opt in only when execution context genuinely does not flow into the code under measurement
    /// and you control everything else touching the database, because any query from another test,
    /// a hosted service or a background seed will be counted against the active budget. For
    /// <c>WebApplicationFactory</c> prefer <c>Server.PreserveExecutionContext = true</c>, which
    /// makes the flow reach the request pipeline and keeps <see cref="AsyncLocalOnly"/> usable.
    /// </remarks>
    SingleActiveScopeFallback = 1
}
