namespace ErickMorales.EntityFrameworkCore.QueryBudget;

/// <summary>
/// A database command captured inside a query budget scope.
/// </summary>
public sealed record RecordedQuery
{
    public required string CommandText { get; init; }

    public IReadOnlyDictionary<string, object?> Parameters { get; init; }
        = new Dictionary<string, object?>();

    public TimeSpan Duration { get; init; }

    public string? Database { get; init; }

    public string? ConnectionId { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
