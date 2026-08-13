namespace EfCoreQueryBudget;

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

    /// <summary>
    /// EF Core's id for the connection the command ran on. Kept as a <see cref="Guid"/> rather than
    /// rendered: capture runs once per command and nothing reads this unless a report asks for it.
    /// </summary>
    public Guid ConnectionId { get; init; }

    /// <summary>
    /// EF Core's correlation id for this command execution. Used to discard a second capture of
    /// the same execution when the interceptor ends up attached more than once.
    /// </summary>
    public Guid CommandId { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
