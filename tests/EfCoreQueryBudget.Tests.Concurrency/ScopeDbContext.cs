using EfCoreQueryBudget;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQueryBudget.Tests.Concurrency;

/// <summary>
/// A private SQLite database per caller, so two scopes running at once share nothing but the
/// process-wide capture state that is under test.
/// </summary>
internal static class ScopeDb
{
    public static ScopeDbContext Create(string name)
    {
        var connection = new SqliteConnection(
            $"Data Source=file:{name}-{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();

        var options = new DbContextOptionsBuilder<ScopeDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new QueryBudgetCommandInterceptor())
            .Options;

        return new ScopeDbContext(options, connection);
    }
}

internal sealed class ScopeItem
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class ScopeDbContext : DbContext
{
    private readonly SqliteConnection _connection;

    public ScopeDbContext(DbContextOptions<ScopeDbContext> options, SqliteConnection connection)
        : base(options)
    {
        _connection = connection;
    }

    public DbSet<ScopeItem> Items => Set<ScopeItem>();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
