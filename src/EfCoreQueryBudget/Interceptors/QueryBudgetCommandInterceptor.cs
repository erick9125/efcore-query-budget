using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace EfCoreQueryBudget;

/// <summary>
/// Captures EF Core database commands into the active <see cref="QueryBudgetContext"/> scope.
/// When no scope is active, returns immediately with negligible overhead.
/// </summary>
public sealed class QueryBudgetCommandInterceptor : DbCommandInterceptor
{
    private readonly IOptions<QueryBudgetLibraryOptions>? _options;

    public QueryBudgetCommandInterceptor()
    {
    }

    public QueryBudgetCommandInterceptor(IOptions<QueryBudgetLibraryOptions> options)
    {
        _options = options;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        TryRecord(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        TryRecord(command, eventData);
        return new ValueTask<DbDataReader>(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<InterceptionResult<object>>(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        TryRecord(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        TryRecord(command, eventData);
        return new ValueTask<object?>(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<InterceptionResult<int>>(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        TryRecord(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        TryRecord(command, eventData);
        return new ValueTask<int>(result);
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        TryRecordFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        TryRecordFailed(command, eventData);
        return Task.CompletedTask;
    }

    private void TryRecord(DbCommand command, CommandExecutedEventData eventData)
    {
        TryRecord(
            command,
            eventData.Duration,
            eventData.Connection,
            eventData.ConnectionId,
            eventData.CommandId);
    }

    private void TryRecordFailed(DbCommand command, CommandErrorEventData eventData)
    {
        TryRecord(
            command,
            eventData.Duration,
            eventData.Connection,
            eventData.ConnectionId,
            eventData.CommandId);
    }

    // Resolves the scope exactly once: checking for one and then recording through a second
    // lookup would take the process-wide lock twice per command and leave a window in which the
    // scope ends in between.
    private void TryRecord(
        DbCommand command,
        TimeSpan duration,
        DbConnection? connection,
        Guid connectionId,
        Guid commandId)
    {
        var settings = _options?.Value;
        if (settings?.Enabled == false)
        {
            return;
        }

        var mode = settings?.AttributionMode ?? ScopeAttributionMode.AsyncLocalOnly;
        if (!QueryBudgetContext.TryGetScope(mode, out var scope))
        {
            return;
        }

        scope.Record(CreateRecordedQuery(
            command,
            duration,
            connection?.Database,
            connectionId.ToString(),
            commandId));
    }

    private static RecordedQuery CreateRecordedQuery(
        DbCommand command,
        TimeSpan duration,
        string? database,
        string? connectionId,
        Guid commandId)
    {
        // Projected now rather than held by reference: the value belongs to the caller and may be
        // large, mutable, or sensitive, and the scope outlives the command by design.
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DbParameter parameter in command.Parameters)
        {
            parameters[parameter.ParameterName] = ParameterCapture.Capture(parameter.Value);
        }

        return new RecordedQuery
        {
            CommandText = command.CommandText,
            Parameters = parameters,
            Duration = duration,
            Database = database,
            ConnectionId = connectionId,
            CommandId = commandId,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
