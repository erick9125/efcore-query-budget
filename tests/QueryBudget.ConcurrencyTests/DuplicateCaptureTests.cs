using ErickMorales.EntityFrameworkCore.QueryBudget;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQueryBudget.ConcurrencyTests;

public sealed class DuplicateCaptureTests
{
    [Fact]
    public async Task An_interceptor_attached_twice_does_not_double_the_metrics()
    {
        await using var connection = new SqliteConnection(
            $"Data Source=file:dup-{Guid.NewGuid():N}?mode=memory&cache=shared");
        await connection.OpenAsync();

        var interceptor = new QueryBudgetCommandInterceptor();
        var options = new DbContextOptionsBuilder<DuplicateDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .AddInterceptors(interceptor)
            .Options;

        await using (var setup = new DuplicateDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = new DuplicateDbContext(options);
            return await db.Items.CountAsync();
        });

        measurement.Metrics.QueryCount.Should().Be(1);
        measurement.Metrics.DuplicateCaptureCount.Should().Be(1);
    }

    [Fact]
    public async Task A_query_running_twice_is_counted_twice()
    {
        // The guard keys off EF Core's per-execution CommandId, so genuine repeats must survive it.
        // Without this, duplicate and N+1 detection would silently collapse to a single execution.
        await using var connection = new SqliteConnection(
            $"Data Source=file:repeat-{Guid.NewGuid():N}?mode=memory&cache=shared");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DuplicateDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new QueryBudgetCommandInterceptor())
            .Options;

        await using (var setup = new DuplicateDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = new DuplicateDbContext(options);
            await db.Items.CountAsync();
            await db.Items.CountAsync();
            return await db.Items.CountAsync();
        });

        measurement.Metrics.QueryCount.Should().Be(3);
        measurement.Metrics.DuplicateCaptureCount.Should().Be(0);
    }

    private sealed class Item
    {
        public int Id { get; set; }
    }

    private sealed class DuplicateDbContext : DbContext
    {
        public DuplicateDbContext(DbContextOptions<DuplicateDbContext> options)
            : base(options)
        {
        }

        public DbSet<Item> Items => Set<Item>();
    }
}
