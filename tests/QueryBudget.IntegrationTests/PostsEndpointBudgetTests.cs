using ErickMorales.EntityFrameworkCore.QueryBudget;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace EfCoreQueryBudget.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("query_budget")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await Container.StartAsync();

    public async Task DisposeAsync() => await Container.DisposeAsync();
}

public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public AppFactory(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            {
                options
                    .UseNpgsql(_postgres.Container.GetConnectionString())
                    .AddInterceptors(
                        serviceProvider.GetRequiredService<QueryBudgetCommandInterceptor>());
            });
        });
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

[Collection("postgres")]
public sealed class PostsEndpointBudgetTests : IAsyncLifetime
{
    private readonly AppFactory _factory;
    private readonly HttpClient _client;

    public PostsEndpointBudgetTests(PostgresFixture postgres)
    {
        _factory = new AppFactory(postgres);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var alice = new Author { Name = "Alice" };
        var bob = new Author { Name = "Bob" };
        db.Authors.AddRange(alice, bob);
        db.Posts.AddRange(
            new Post { Title = "One", Author = alice },
            new Post { Title = "Two", Author = alice },
            new Post { Title = "Three", Author = bob },
            new Post { Title = "Four", Author = bob },
            new Post { Title = "Five", Author = alice });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Optimized_endpoint_stays_within_budget()
    {
        await QueryBudget.AssertAsync(
            new QueryBudgetOptions
            {
                MaxQueries = 4,
                MaxExactDuplicates = 0,
                ScopeLabel = "GET /api/posts/optimized"
            },
            async () =>
            {
                var response = await _client.GetAsync("/api/posts/optimized");
                response.EnsureSuccessStatusCode();
            });
    }

    [Fact]
    public async Task Problematic_endpoint_exceeds_budget_and_shows_possible_n_plus_one()
    {
        var act = async () => await QueryBudget.AssertAsync(
            new QueryBudgetOptions
            {
                MaxQueries = 4,
                MaxRepeatedPatterns = 0,
                RepeatedPatternThreshold = 3,
                ScopeLabel = "GET /api/posts/problematic"
            },
            async () =>
            {
                var response = await _client.GetAsync("/api/posts/problematic");
                response.EnsureSuccessStatusCode();
            });

        var exception = await act.Should().ThrowAsync<QueryBudgetExceededException>();
        exception.Which.Result.Metrics.QueryCount.Should().BeGreaterThan(4);
        exception.Which.Message.Should().Contain("EF Core query budget exceeded");
        exception.Which.Message.Should().Contain("Possible N+1 query pattern");
    }

    [Fact]
    public async Task MeasureAsync_reports_query_counts_for_both_endpoints()
    {
        var problematic = await QueryBudget.MeasureAsync(async () =>
        {
            var response = await _client.GetAsync("/api/posts/problematic");
            response.EnsureSuccessStatusCode();
        });

        var optimized = await QueryBudget.MeasureAsync(async () =>
        {
            var response = await _client.GetAsync("/api/posts/optimized");
            response.EnsureSuccessStatusCode();
        });

        problematic.Metrics.QueryCount.Should().BeGreaterThan(optimized.Metrics.QueryCount);
        optimized.Metrics.QueryCount.Should().BeLessThanOrEqualTo(4);
    }
}
