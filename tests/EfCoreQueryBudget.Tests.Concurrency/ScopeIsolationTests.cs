using EfCoreQueryBudget;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQueryBudget.Tests.Concurrency;

/// <summary>
/// Isolation between scopes started on the same test.
/// </summary>
/// <remarks>
/// Deliberately a separate class from <see cref="ParallelClassIsolationTests"/>. xUnit runs one
/// class as one collection, sequentially, so a single class can only ever exercise concurrency it
/// creates itself. Two classes run at the same time under xUnit's own scheduler, which is the
/// scenario the attribution fix was about: unrelated test code holding a budget while this one does.
/// </remarks>
public class ScopeIsolationTests
{
    [Fact]
    public async Task Concurrent_scopes_only_see_their_own_queries()
    {
        var first = Task.Run(() =>
            QueryBudget.MeasureAsync(async () =>
            {
                await using var db = ScopeDb.Create("service-a");
                await db.Database.EnsureCreatedAsync();
                db.Items.Add(new ScopeItem { Name = "a-1" });
                db.Items.Add(new ScopeItem { Name = "a-2" });
                await db.SaveChangesAsync();
                return await db.Items.CountAsync();
            }));

        var second = Task.Run(() =>
            QueryBudget.MeasureAsync(async () =>
            {
                await using var db = ScopeDb.Create("service-b");
                await db.Database.EnsureCreatedAsync();
                db.Items.Add(new ScopeItem { Name = "b-1" });
                await db.SaveChangesAsync();
                // The string overload, not the char one: EF Core translates this to SQL and has no
                // translation for StartsWith(char), whatever CA1866 says about the runtime cost.
                _ = await db.Items.Where(x => x.Name.StartsWith("b")).ToListAsync();
                return await db.Items.CountAsync();
            }));

        var results = await Task.WhenAll(first, second);

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
            ScopeRun.MeasureAsync("request-a", expectedCount: 1),
            ScopeRun.MeasureAsync("request-b", expectedCount: 2),
            ScopeRun.MeasureAsync("request-c", expectedCount: 3));
    }
}

/// <summary>
/// The same guarantee, from a class that xUnit schedules alongside
/// <see cref="ScopeIsolationTests"/>. Both loop long enough to overlap, so each is running budgeted
/// database work while the other is.
/// </summary>
public class ParallelClassIsolationTests
{
    [Fact]
    public async Task A_scope_stays_isolated_from_another_test_class_running_at_the_same_time()
    {
        for (var round = 0; round < 20; round++)
        {
            await ScopeRun.MeasureAsync($"parallel-{round}", expectedCount: 2);
        }
    }

    [Fact]
    public async Task Unbudgeted_work_beside_a_budget_is_not_captured()
    {
        // The default attribution mode follows the execution flow, so this must stay at zero even
        // while other classes are measuring.
        var background = Task.Run(async () =>
        {
            await using var db = ScopeDb.Create("no-budget");
            await db.Database.EnsureCreatedAsync();
            db.Items.Add(new ScopeItem { Name = "background" });
            await db.SaveChangesAsync();
        });

        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await background;
            return 0;
        });

        measurement.Metrics.QueryCount.Should().Be(0);
    }
}

internal static class ScopeRun
{
    public static async Task MeasureAsync(string marker, int expectedCount)
    {
        var measurement = await QueryBudget.MeasureAsync(async () =>
        {
            await using var db = ScopeDb.Create(marker);
            await db.Database.EnsureCreatedAsync();
            for (var i = 0; i < expectedCount; i++)
            {
                db.Items.Add(new ScopeItem { Name = $"{marker}-{i}" });
            }

            await db.SaveChangesAsync();
            return await db.Items.CountAsync();
        });

        measurement.Value.Should().Be(expectedCount);
        measurement.Metrics.Queries.Should().NotBeEmpty();

        // No other marker's rows leak in, whatever else is measuring at the same time.
        var prefix = marker[..marker.IndexOf('-', StringComparison.Ordinal)];
        measurement.Metrics.Queries.Select(q => q.CommandText)
            .Should().NotContain(sql => sql.Contains(prefix, StringComparison.Ordinal)
                && !sql.Contains(marker, StringComparison.Ordinal));
    }
}
