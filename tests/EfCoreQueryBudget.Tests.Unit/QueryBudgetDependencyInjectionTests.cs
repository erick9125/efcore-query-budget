using EfCoreQueryBudget;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EfCoreQueryBudget.Tests.Unit;

/// <summary>
/// The wiring the README teaches, assembled: registration, resolution and a real measurement
/// through a container-built <c>DbContext</c>.
/// </summary>
public sealed class QueryBudgetDependencyInjectionTests
{
    [Fact]
    public async Task The_registered_interceptor_captures_through_a_container_built_context()
    {
        using var connection = OpenConnection();
        await using var provider = BuildProvider(connection);

        await EnsureCreated(provider);

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DiDbContext>();
            return await db.Items.ToListAsync();
        });

        measurement.Metrics.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task Disabling_capture_through_the_options_callback_reaches_the_interceptor()
    {
        using var connection = OpenConnection();
        await using var provider = BuildProvider(connection, options => options.Enabled = false);

        await EnsureCreated(provider);

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DiDbContext>();
            return await db.Items.ToListAsync();
        });

        measurement.Metrics.QueryCount.Should().Be(0);
    }

    [Fact]
    public async Task A_budget_assertion_fails_through_the_container()
    {
        using var connection = OpenConnection();
        await using var provider = BuildProvider(connection);

        await EnsureCreated(provider);

        var act = async () => await QueryBudget.AssertAsync(
            new QueryBudgetOptions { MaxQueries = 0, ScopeLabel = "di" },
            async () =>
            {
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DiDbContext>();
                await db.Items.ToListAsync();
            });

        var exception = await act.Should().ThrowAsync<QueryBudgetExceededException>();
        exception.Which.Message.Should().Contain("Scope: di");
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(
            $"Data Source=file:di-{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }

    private static ServiceProvider BuildProvider(
        SqliteConnection connection,
        Action<QueryBudgetLibraryOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddEfCoreQueryBudget(configure);

        services.AddDbContext<DiDbContext>((serviceProvider, builder) =>
            builder
                .UseSqlite(connection)
                .AddInterceptors(
                    serviceProvider.GetRequiredService<QueryBudgetCommandInterceptor>()));

        return services.BuildServiceProvider();
    }

    private static async Task EnsureCreated(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DiDbContext>()
            .Database.EnsureCreatedAsync();
    }

    private sealed class DiDbContext : DbContext
    {
        public DiDbContext(DbContextOptions<DiDbContext> options)
            : base(options)
        {
        }

        public DbSet<Item> Items => Set<Item>();
    }

    private sealed class Item
    {
        public int Id { get; set; }
    }
}
