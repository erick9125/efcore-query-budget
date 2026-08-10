using ErickMorales.EntityFrameworkCore.QueryBudget;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQueryBudget.ConcurrencyTests;

public class ScopeIsolationTests
{
    [Fact]
    public async Task Concurrent_scopes_only_see_their_own_queries()
    {
        var first = Task.Run(() =>
            QueryBudget.MeasureAsync(async () =>
            {
                await using var db = CreateContext("service-a");
                await db.Database.EnsureCreatedAsync();
                db.Items.Add(new Item { Name = "a-1" });
                db.Items.Add(new Item { Name = "a-2" });
                await db.SaveChangesAsync();
                return await db.Items.CountAsync();
            }));

        var second = Task.Run(() =>
            QueryBudget.MeasureAsync(async () =>
            {
                await using var db = CreateContext("service-b");
                await db.Database.EnsureCreatedAsync();
                db.Items.Add(new Item { Name = "b-1" });
                await db.SaveChangesAsync();
                _ = await db.Items.Where(x => x.Name.StartsWith("b")).ToListAsync();
                return await db.Items.CountAsync();
            }));

        var results = await Task.WhenAll(first, second);

        results[0].Metrics.Queries.Should().OnlyContain(q =>
            q.CommandText.Contains("Items", StringComparison.OrdinalIgnoreCase)
            || q.CommandText.Contains("sqlite_master", StringComparison.OrdinalIgnoreCase)
            || q.CommandText.Contains("CREATE", StringComparison.OrdinalIgnoreCase));
        results[1].Metrics.Queries.Should().OnlyContain(q =>
            q.CommandText.Contains("Items", StringComparison.OrdinalIgnoreCase)
            || q.CommandText.Contains("sqlite_master", StringComparison.OrdinalIgnoreCase)
            || q.CommandText.Contains("CREATE", StringComparison.OrdinalIgnoreCase));

        var aSql = string.Join('\n', results[0].Metrics.Queries.Select(q => q.CommandText));
        var bSql = string.Join('\n', results[1].Metrics.Queries.Select(q => q.CommandText));

        aSql.Should().NotContain("b-1");
        bSql.Should().NotContain("a-1");
        results[0].Metrics.QueryCount.Should().BeGreaterThan(0);
        results[1].Metrics.QueryCount.Should().BeGreaterThan(0);
        results[0].Value.Should().Be(2);
        results[1].Value.Should().Be(1);
    }

    [Fact]
    public async Task Task_WhenAll_keeps_three_scopes_isolated()
    {
        await Task.WhenAll(
            RunBudgetScopeAsync("request-a", expectedCount: 1),
            RunBudgetScopeAsync("request-b", expectedCount: 2),
            RunBudgetScopeAsync("request-c", expectedCount: 3));
    }

    private static async Task RunBudgetScopeAsync(string marker, int expectedCount)
    {
        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = CreateContext(marker);
            await db.Database.EnsureCreatedAsync();
            for (var i = 0; i < expectedCount; i++)
            {
                db.Items.Add(new Item { Name = $"{marker}-{i}" });
            }

            await db.SaveChangesAsync();
            return await db.Items.CountAsync();
        });

        measurement.Value.Should().Be(expectedCount);
        measurement.Metrics.Queries.Should().NotBeEmpty();
        measurement.Metrics.Queries.Select(q => q.CommandText)
            .Should().NotContain(sql => sql.Contains("request-", StringComparison.Ordinal)
                && !sql.Contains(marker, StringComparison.Ordinal));
    }

    private static ConcurrencyDbContext CreateContext(string name)
    {
        var connection = new SqliteConnection($"Data Source=file:{name}-{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();

        var interceptor = new QueryBudgetCommandInterceptor();
        var options = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        return new ConcurrencyDbContext(options, connection);
    }

    private sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ConcurrencyDbContext : DbContext
    {
        private readonly SqliteConnection _connection;

        public ConcurrencyDbContext(
            DbContextOptions<ConcurrencyDbContext> options,
            SqliteConnection connection)
            : base(options)
        {
            _connection = connection;
        }

        public DbSet<Item> Items => Set<Item>();

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
