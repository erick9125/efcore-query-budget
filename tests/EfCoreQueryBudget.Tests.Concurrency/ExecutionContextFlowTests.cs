using System.Globalization;
using EfCoreQueryBudget;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EfCoreQueryBudget.Tests.Concurrency;

/// <summary>
/// Pins down whether the execution flow reaches an in-process HTTP pipeline, which is what
/// decides if <see cref="ScopeAttributionMode.AsyncLocalOnly"/> is usable for endpoint budgets.
/// </summary>
public sealed class ExecutionContextFlowTests
{
    [Fact]
    public async Task Preserved_execution_context_attributes_endpoint_queries_to_the_scope()
    {
        using var host = new TestHostFixture(preserveExecutionContext: true);
        using var client = host.Server.CreateClient();

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
        });

        measurement.Metrics.QueryCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Without_preserved_execution_context_endpoint_queries_are_not_attributed()
    {
        // This is the gap the process-wide fallback used to paper over: TestServer suppresses the
        // flow by default, so the request pipeline runs without the caller's AsyncLocal scope.
        using var host = new TestHostFixture(preserveExecutionContext: false);
        using var client = host.Server.CreateClient();

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
        });

        measurement.Metrics.QueryCount.Should().Be(0);
    }

    [Fact]
    public async Task Unscoped_database_work_never_leaks_into_an_active_budget()
    {
        using var host = new TestHostFixture(preserveExecutionContext: true);
        using var client = host.Server.CreateClient();

        // Started before any scope exists, so its execution flow provably carries none — the same
        // position a hosted service or a parallel test is in. It is gated so the requests actually
        // overlap the measured one.
        var start = new TaskCompletionSource();
        var unscopedWork = Task.Run(async () =>
        {
            await start.Task;
            using var unscoped = host.Server.CreateClient();
            for (var i = 0; i < 5; i++)
            {
                (await unscoped.GetAsync("/")).EnsureSuccessStatusCode();
            }
        });

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            start.SetResult();
            var response = await client.GetAsync("/");
            response.EnsureSuccessStatusCode();
            await unscopedWork;
        });

        // One command for the scoped request; the five unscoped ones must not be counted.
        measurement.Metrics.QueryCount.Should().Be(1);
    }

    private sealed class TestHostFixture : IDisposable
    {
        private readonly SqliteConnection _keepAlive;

        public TestHostFixture(bool preserveExecutionContext)
        {
            // Every context opens its own connection to the same shared-cache database, rather than
            // sharing one instance. SqliteConnection is not thread-safe, and these tests overlap
            // requests on purpose: a shared instance corrupts its internal command list under load.
            // The connection below is only held open so the in-memory database outlives the
            // requests that touch it.
            var connectionString =
                $"Data Source=file:flow-{Guid.NewGuid():N}?mode=memory&cache=shared";

            _keepAlive = new SqliteConnection(connectionString);
            _keepAlive.Open();

            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddEfCoreQueryBudget();
                    services.AddDbContext<FlowDbContext>((serviceProvider, options) => options
                        .UseSqlite(connectionString)
                        .AddInterceptors(
                            serviceProvider.GetRequiredService<QueryBudgetCommandInterceptor>()));
                })
                .Configure(app => app.Run(async context =>
                {
                    var db = context.RequestServices.GetRequiredService<FlowDbContext>();
                    var count = await db.Items.CountAsync();
                    await context.Response.WriteAsync(
                        count.ToString(CultureInfo.InvariantCulture));
                }));

            Server = new TestServer(builder)
            {
                PreserveExecutionContext = preserveExecutionContext
            };

            using var scope = Server.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<FlowDbContext>()
                .Database.EnsureCreated();
        }

        public TestServer Server { get; }

        public void Dispose()
        {
            Server.Dispose();
            _keepAlive.Dispose();
        }
    }

    private sealed class Item
    {
        public int Id { get; set; }
    }

    private sealed class FlowDbContext : DbContext
    {
        public FlowDbContext(DbContextOptions<FlowDbContext> options)
            : base(options)
        {
        }

        public DbSet<Item> Items => Set<Item>();
    }
}
