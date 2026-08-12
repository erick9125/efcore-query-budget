using System.Data.Common;
using EfCoreQueryBudget;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace EfCoreQueryBudget.Tests.Unit;

/// <summary>
/// Exercises the interceptor through a real EF Core stack on in-memory SQLite, so command ids,
/// durations and parameters are the ones EF Core actually produces.
/// </summary>
public sealed class InterceptorCaptureTests
{
    [Fact]
    public async Task A_reader_is_captured_asynchronously()
    {
        using var host = CaptureHost.Create();

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = host.NewContext();
            return await db.Items.ToListAsync();
        });

        measurement.Metrics.QueryCount.Should().Be(1);
        measurement.Metrics.Queries[0].CommandText.Should().Contain("SELECT");
    }

    [Fact]
    public async Task A_reader_is_captured_synchronously()
    {
        using var host = CaptureHost.Create();

        var measurement = await QueryBudget.MeasureAsync(() =>
        {
            using var db = host.NewContext();
            return Task.FromResult(db.Items.ToList());
        });

        measurement.Metrics.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task A_non_query_is_captured_asynchronously()
    {
        using var host = CaptureHost.Create();

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = host.NewContext();
            return await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Items (Name) VALUES ('one')");
        });

        measurement.Metrics.QueryCount.Should().Be(1);
        measurement.Metrics.Queries[0].CommandText.Should().Contain("INSERT");
    }

    [Fact]
    public async Task A_non_query_is_captured_synchronously()
    {
        using var host = CaptureHost.Create();

        var measurement = await QueryBudget.MeasureAsync(() =>
        {
            using var db = host.NewContext();
            return Task.FromResult(
                db.Database.ExecuteSqlRaw("INSERT INTO Items (Name) VALUES ('one')"));
        });

        measurement.Metrics.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task A_failed_command_is_captured_and_the_error_still_surfaces()
    {
        using var host = CaptureHost.Create();
        QueryMetrics? metrics = null;

        var act = async () => await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = host.NewContext();
            return await db.Database.ExecuteSqlRawAsync("SELECT * FROM does_not_exist");
        });

        await act.Should().ThrowAsync<SqliteException>();

        // The command is recorded on the way out, so a failing query still counts against the
        // budget of whatever scope was active.
        metrics = await FailingScopeMetrics(host, async db =>
            await db.Database.ExecuteSqlRawAsync("SELECT * FROM does_not_exist"));

        metrics.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task A_failed_command_is_captured_synchronously()
    {
        using var host = CaptureHost.Create();

        var metrics = await FailingScopeMetrics(host, db =>
        {
            db.Database.ExecuteSqlRaw("SELECT * FROM does_not_exist");
            return Task.FromResult(0);
        });

        metrics.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task Nothing_is_captured_outside_a_scope()
    {
        using var host = CaptureHost.Create();

        await using (var db = host.NewContext())
        {
            await db.Items.ToListAsync();
        }

        var measurement = await QueryBudget.MeasureAsync(() => Task.FromResult(0));
        measurement.Metrics.QueryCount.Should().Be(0);
    }

    [Fact]
    public async Task Nothing_is_captured_when_capture_is_disabled()
    {
        using var host = CaptureHost.Create(new QueryBudgetLibraryOptions { Enabled = false });

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = host.NewContext();
            return await db.Items.ToListAsync();
        });

        measurement.Metrics.QueryCount.Should().Be(0);
    }

    [Fact]
    public async Task The_attribution_mode_from_the_host_options_is_honoured()
    {
        using var host = CaptureHost.Create(new QueryBudgetLibraryOptions
        {
            AttributionMode = ScopeAttributionMode.SingleActiveScopeFallback
        });

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            // Runs on its own flow, so only the fallback can attribute it to the scope.
            return await Task.Run(async () =>
            {
                await using var db = host.NewContext();
                return await db.Items.ToListAsync();
            });
        });

        measurement.Metrics.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task A_captured_command_carries_its_context()
    {
        using var host = CaptureHost.Create();

        // A captured variable, not a literal: EF Core inlines constants and only parameterizes
        // values that come from outside the expression.
        var name = "missing";

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = host.NewContext();
            return await db.Items.Where(i => i.Name == name).ToListAsync();
        });

        var query = measurement.Metrics.Queries.Should().ContainSingle().Subject;
        query.CommandId.Should().NotBe(Guid.Empty);
        query.ConnectionId.Should().NotBeNullOrEmpty();
        query.Database.Should().NotBeNullOrEmpty();
        query.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        query.Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_binary_parameter_is_projected_rather_than_referenced()
    {
        using var host = CaptureHost.Create();
        var payload = new byte[512];

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = host.NewContext();
            return await db.Items
                .Where(i => i.Payload == payload)
                .ToListAsync();
        });

        // End-to-end proof that the capture path projects: the scope holds a snapshot, not the
        // caller's array.
        var value = measurement.Metrics.Queries
            .Should().ContainSingle().Subject
            .Parameters.Values.Should().ContainSingle().Subject;

        value.Should().BeOfType<ParameterSnapshot>()
            .Which.TypeName.Should().Be("byte[512]");
    }

    [Fact]
    public void A_scalar_execution_is_captured()
    {
        // No public EF Core API reaches ExecuteScalar deterministically, so this drives the
        // override directly. It is the only test here that is not a real interception.
        using var host = CaptureHost.Create();
        using var handle = QueryBudgetContext.Begin();
        var scope = QueryBudgetContext.Current!;

        using var command = host.NewCommand("SELECT 1");
        new QueryBudgetCommandInterceptor()
            .ScalarExecuted(command, ExecutedEventData(command), result: 1);

        scope.Snapshot().Should().ContainSingle()
            .Which.CommandText.Should().Be("SELECT 1");
    }

    [Fact]
    public async Task A_scalar_execution_is_captured_asynchronously()
    {
        using var host = CaptureHost.Create();
        using var handle = QueryBudgetContext.Begin();
        var scope = QueryBudgetContext.Current!;

        using var command = host.NewCommand("SELECT 1");
        await new QueryBudgetCommandInterceptor()
            .ScalarExecutedAsync(command, ExecutedEventData(command), result: 1);

        scope.Snapshot().Should().ContainSingle();
    }

    private static CommandExecutedEventData ExecutedEventData(DbCommand command)
    {
        // Only Duration, Connection, ConnectionId and CommandId are read by the interceptor; the
        // logging members are never touched, so they are left null.
        return new CommandExecutedEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: command.Connection!,
            command: command,
            context: null,
            executeMethod: DbCommandMethod.ExecuteScalar,
            commandId: Guid.NewGuid(),
            connectionId: Guid.NewGuid(),
            result: 1,
            async: false,
            logParameterValues: false,
            startTime: DateTimeOffset.UnixEpoch,
            duration: TimeSpan.FromMilliseconds(3),
            commandSource: CommandSource.Unknown);
    }

    private static async Task<QueryMetrics> FailingScopeMetrics(
        CaptureHost host,
        Func<CaptureDbContext, Task<int>> action)
    {
        // MeasureAsync propagates the failure before it can build the metrics, so the scope is
        // driven directly to see what the interceptor recorded.
        using var handle = QueryBudgetContext.Begin();
        var scope = QueryBudgetContext.Current!;

        try
        {
            await using var db = host.NewContext();
            await action(db);
        }
        catch (SqliteException)
        {
            // Expected: the point is what was captured on the way out.
        }

        return new QueryMetricsCalculator().Calculate(
            scope.Snapshot(),
            new QueryBudgetOptions(),
            scope.Totals);
    }

    private sealed class CaptureHost : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<CaptureDbContext> _options;

        private CaptureHost(SqliteConnection connection, DbContextOptions<CaptureDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static CaptureHost Create(QueryBudgetLibraryOptions? libraryOptions = null)
        {
            var connection = new SqliteConnection(
                $"Data Source=file:capture-{Guid.NewGuid():N}?mode=memory&cache=shared");
            connection.Open();

            var interceptor = libraryOptions is null
                ? new QueryBudgetCommandInterceptor()
                : new QueryBudgetCommandInterceptor(Options.Create(libraryOptions));

            var options = new DbContextOptionsBuilder<CaptureDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;

            using (var setup = new CaptureDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            return new CaptureHost(connection, options);
        }

        public CaptureDbContext NewContext() => new(_options);

        public DbCommand NewCommand(string sql)
        {
            var command = _connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        public void Dispose() => _connection.Dispose();
    }

    private sealed class CaptureDbContext : DbContext
    {
        public CaptureDbContext(DbContextOptions<CaptureDbContext> options)
            : base(options)
        {
        }

        public DbSet<Item> Items => Set<Item>();
    }

    private sealed class Item
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public byte[]? Payload { get; set; }
    }
}
