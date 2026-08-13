# EF Core Query Budget

[![CI](https://img.shields.io/badge/ci-GitHub%20Actions-blue)](.github/workflows/ci.yml)
[![NuGet](https://img.shields.io/badge/nuget-erick9125.EfCoreQueryBudget-blue)](https://www.nuget.org/packages/erick9125.EfCoreQueryBudget)
[![Target](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

Define and enforce **database query budgets** in your EF Core tests.

Capture SQL commands inside isolated execution scopes and fail fast when an endpoint, service, or repository quietly regresses into too many queries, exact duplicates, repeated patterns, or slow database work — before that cost reaches production.

> **Spanish docs:** [README.es.md](README.es.md)

---

## Promise (0.1.0)

> Capture EF Core database commands inside isolated test scopes and enforce configurable budgets for query count, exact duplicates, repeated patterns, slow queries, and total database time.

---

## The problem

Functional tests often prove that an API still returns `200 OK`. They rarely prove that the database work behind that response stayed cheap.

| | Before | After a change |
|---|---|---|
| `GET /orders` | 3 queries · 35 ms | 31 queries · 280 ms |
| Result | Tests pass | Tests still pass |

ORM convenience hides expensive access patterns:

- an `Include` removed “temporarily”
- a loop that loads related rows one by one
- the same query repeated with identical parameters
- slow round trips that only show up with real data

**EF Core Query Budget** turns that silent regression into a verifiable condition.

```text
EF Core query budget exceeded

Scope: GET /api/orders

Query count
  Budget: <= 5
  Actual:   31

Exact duplicates
  Budget: <= 0
  Actual:   4

Repeated query patterns
  Budget: <= 1
  Actual:   1

Possible N+1 query pattern.
```

---

## What this library is for

Use it when you want database performance to be part of your test contract:

| Use case | Example |
|---|---|
| Service / use-case tests | Assert a use case stays within a query budget |
| Repository tests | Catch accidental query fan-out in data access |
| Integration tests | Measure real EF Core SQL against PostgreSQL |
| Endpoint tests | Wrap `WebApplicationFactory` HTTP calls |

It is **not** an APM, dashboard, or production profiler. It is a focused testing and diagnostics tool for EF Core.

---

## Features

| Feature | Behavior |
|---|---|
| Command capture | `DbCommandInterceptor` for Reader / Scalar / NonQuery (sync + async) |
| Isolated scopes | `AsyncLocal` measurement scopes per execution flow |
| Query count | Total commands attributed to the active scope |
| Exact duplicates | Same SQL + same parameter values, repeated |
| Repeated patterns | Same SQL + different parameter sets (possible N+1) |
| Slow queries | Count commands at or above a duration threshold |
| Duration budgets | Total DB time and worst single query |
| Assert or measure | Fail the test, or only collect metrics |
| Safe reports | Parameter values hidden by default |
| ASP.NET Core ready | Works with DI + `WebApplicationFactory` |

---

## Install

```bash
dotnet add package erick9125.EfCoreQueryBudget
```

**Requirements:** .NET 8 or .NET 9, with the matching EF Core major (8.x or 9.x). ASP.NET Core is optional — the library works in service and repository tests without a web host.

---

## Quick start

### 1. Register the interceptor

```csharp
using EfCoreQueryBudget;
using Microsoft.EntityFrameworkCore;

builder.Services.AddEfCoreQueryBudget();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddInterceptors(
            serviceProvider.GetRequiredService<QueryBudgetCommandInterceptor>());
});
```

Registration only controls **capture**. Thresholds and limits belong to `QueryBudgetOptions` and
are supplied per assertion, since two budgets in the same suite rarely want the same numbers.
The one knob here is `Enabled`:

```csharp
builder.Services.AddEfCoreQueryBudget(options =>
{
    options.Enabled = !builder.Environment.IsProduction();
});
```

### 2. Assert a budget in a test

```csharp
using EfCoreQueryBudget;

await QueryBudget.AssertAsync(
    new QueryBudgetOptions
    {
        MaxQueries = 5,
        MaxExactDuplicates = 0,
        ScopeLabel = "GET /api/orders"
    },
    async () =>
    {
        await client.GetAsync("/api/orders");
    });
```

---

## Usage examples

### Assert against a service

```csharp
await QueryBudget.AssertAsync(
    new QueryBudgetOptions
    {
        MaxQueries = 5,
        MaxExactDuplicates = 0,
        MaxRepeatedPatterns = 1,
        MaxTotalDuration = TimeSpan.FromMilliseconds(150),
        ScopeLabel = "OrderService.GetOrdersAsync"
    },
    async () =>
    {
        await orderService.GetOrdersAsync();
    });
```

### Measure without failing

Useful while establishing a baseline or debugging a hot path:

```csharp
var measurement = await QueryBudget.MeasureAsync(async () =>
{
    await orderService.GetOrdersAsync();
});

Console.WriteLine(measurement.Metrics.QueryCount);
Console.WriteLine(measurement.Metrics.RedundantExecutionCount);
Console.WriteLine(measurement.Metrics.RepeatedPatternCount);
Console.WriteLine(measurement.Metrics.TotalDuration);
```

### Assert and keep the result

`AssertAsync` also comes in a form that returns what the action produced, so a budget can wrap a call
without splitting it in two. Both forms take an optional `CancellationToken`.

```csharp
var orders = await QueryBudget.AssertAsync(
    new QueryBudgetOptions { MaxQueries = 5 },
    () => orderService.GetOrdersAsync(),
    cancellationToken);
```

### HTTP endpoint test with WebApplicationFactory

```csharp
public class OrdersTests
{
    private readonly HttpClient _client;

    public OrdersTests(AppFactory factory)
    {
        // TestServer does not flow the caller's execution context into the request pipeline
        // by default, so the budget would see zero queries. Set this before CreateClient().
        factory.Server.PreserveExecutionContext = true;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Orders_endpoint_stays_within_budget()
    {
        await QueryBudget.AssertAsync(
            new QueryBudgetOptions
            {
                MaxQueries = 4,
                MaxExactDuplicates = 0,
                ScopeLabel = "GET /api/orders"
            },
            async () =>
            {
                var response = await _client.GetAsync("/api/orders");
                response.EnsureSuccessStatusCode();
            });
    }
}
```

Commands are attributed strictly by execution flow, so budgets stay correct when tests run in
parallel or a hosted service touches the database at the same time. `PreserveExecutionContext` is
what makes that flow reach the request pipeline. See [docs/concurrency.md](docs/concurrency.md).

### Catch a possible N+1

```csharp
// Problematic: 1 query for posts + N queries for authors
var posts = await context.Posts.ToListAsync();
foreach (var post in posts)
{
    post.Author = await context.Authors
        .SingleAsync(x => x.Id == post.AuthorId);
}

// Optimized: 1 query
var posts = await context.Posts
    .Include(x => x.Author)
    .ToListAsync();
```

A budget like `MaxQueries = 4` / `MaxRepeatedPatterns = 0` fails the problematic path and passes the optimized one. The sample app under `samples/AspNetCorePostgres` demonstrates both endpoints.

---

## Budget options

All limits are optional. Configure only what you want to enforce.

```csharp
new QueryBudgetOptions
{
    MaxQueries = 5,
    MaxExactDuplicates = 0,
    MaxRepeatedPatterns = 1,
    MaxExecutionsPerPattern = 10,
    MaxSlowQueries = 0,
    MaxTotalDuration = TimeSpan.FromMilliseconds(150),
    MaxSingleQueryDuration = TimeSpan.FromMilliseconds(80),
    SlowQueryThreshold = TimeSpan.FromMilliseconds(100),
    RepeatedPatternThreshold = 5,
    SqlNormalization = SqlNormalizationMode.WhitespaceOnly,
    MaxRecordedQueries = 10_000,
    ScopeLabel = "GET /api/orders",
    ParameterDisplayMode = QueryParameterDisplayMode.Hidden
}
```

| Option | Meaning |
|---|---|
| `MaxQueries` | Maximum commands in the scope |
| `MaxExactDuplicates` | Maximum redundant exact executions |
| `MaxRepeatedPatterns` | Maximum repeated-pattern groups — how many places |
| `MaxExecutionsPerPattern` | Executions in the largest pattern — how big the worst one is |
| `MaxSlowQueries` | Maximum commands ≥ `SlowQueryThreshold` |
| `MaxTotalDuration` | Sum of command durations |
| `MaxSingleQueryDuration` | Worst single command |
| `RepeatedPatternThreshold` | Minimum executions before a pattern counts (default `5`) |
| `SqlNormalization` | How SQL is normalized before grouping (default `WhitespaceOnly`) |
| `MaxRecordedQueries` | Queries retained for analysis (default `10_000`, `null` for no limit) |
| `ScopeLabel` | Shown in the failure report |

`MaxRecordedQueries` bounds memory, not the budget. Past the cap, commands are still counted and
timed — `QueryCount`, `TotalDuration`, `MaximumDuration` and `SlowQueryCount` always cover everything
that ran, so a budget can never pass because the scope stopped looking. Only the duplicate and
pattern groups are built from the retained sample, and the report says how many were left out.

### Raw SQL and inline literals

Patterns are grouped by normalized SQL, and the default normalization only collapses whitespace. So
if your SQL carries inline literals instead of parameters — raw SQL, `FromSqlRaw`, or constants the
provider inlines — each execution looks like a different query, and **no pattern is detected**:

```text
SELECT * FROM posts WHERE author_id = 1
SELECT * FROM posts WHERE author_id = 2
SELECT * FROM posts WHERE author_id = 3
```

Set `SqlNormalization` to mask the literals and those become one pattern:

```csharp
await QueryBudget.AssertAsync(
    new QueryBudgetOptions
    {
        MaxRepeatedPatterns = 0,
        SqlNormalization = SqlNormalizationMode.MaskLiterals
    },
    async () => await service.GetFeedAsync());
```

It masks string and numeric literals and collapses `IN (1, 2, 3)` to `IN (?)`, leaving parameters,
quoted identifiers, `NULL` and comments alone. Exact-duplicate detection is unaffected: it never
masks, so two queries differing in a value stay two queries. The trade-off is that a literal which
carries meaning collapses too, so `LIMIT 10` and `LIMIT 20` become one pattern. Details in
[docs/query-fingerprints.md](docs/query-fingerprints.md).

---

## Metrics

`QueryMetrics` returned by `MeasureAsync` / attached to failures:

| Metric | Meaning |
|---|---|
| `QueryCount` | Commands attributed to the scope |
| `RedundantExecutionCount` | Read executions that were not needed |
| `RepeatedPatternCount` | Repeated read-pattern groups |
| `MaximumPatternExecutions` | Executions in the largest repeated read pattern |
| `SlowQueryCount` | Commands at or above the slow threshold |
| `TotalDuration` | Sum of command durations |
| `MaximumDuration` | Slowest single command |
| `ExactDuplicateGroups` | Grouped exact duplicates, each tagged with its `Operation` |
| `RepeatedPatternGroups` | Grouped structural patterns, each tagged with its `Operation` |

### Reads and writes

Duplicate and pattern budgets apply to **reads only**. Running the same `SELECT` with the same
parameters twice returns the same rows, so the second execution is provably wasted. Running the same
`INSERT` twice is not: it adds two rows, and `UPDATE counters SET n = n + 1` applied twice counts
twice. A `SaveChanges` over 50 new entities emits 50 executions of one `INSERT` shape — the exact
signature of an N+1, and not a defect.

So writes and everything that is neither a read nor a write (session settings, transaction control,
DDL) stay out of `RedundantExecutionCount` and `RepeatedPatternCount`. They are still detected and still
shown, under their own heading and never labelled a possible N+1:

```text
Repeated write (not counted against the budget)
INSERT INTO posts (title) VALUES (@p)
Executions: 50
```

Every group carries `QueryGroup.Operation`, so you can apply your own rule if you need one.

---

## Exact duplicates vs repeated patterns

**Exact duplicate** — same normalized SQL and same parameter values:

```text
SELECT ... FROM users WHERE id = @__id_0
@__id_0 = 10   (repeated)
```

Usually wasted work: cache it, batch it, or stop calling it twice.

**Repeated pattern** — same SQL shape, different parameter sets:

```text
@__id_0 = 10
@__id_0 = 11
@__id_0 = 12
...
```

Often a possible N+1. Reports say:

```text
Possible N+1 query pattern
Executions: 15
Distinct variants: 15
```

Never `N+1 confirmed`. The signal is strong enough to investigate, not strong enough to prove intent. Details: [docs/possible-n-plus-one.md](docs/possible-n-plus-one.md).

---

## Parameter security

Query parameters may contain emails, tokens, identifiers, or passwords.

By default, reports show counts only:

```text
Distinct variants: 12
```

| Mode | Behavior |
|---|---|
| `Hidden` (default) | Counts only |
| `TypesOnly` | Names and CLR types |
| `Full` | Values — local diagnostics only |

Binary payloads are hashed for fingerprinting and never dumped into reports. See [docs/parameter-security.md](docs/parameter-security.md).

---

## How capture works

```text
EF Core command
      │
      ▼
QueryBudgetCommandInterceptor
      │
      ├─ no active scope  → return immediately (near-zero overhead)
      └─ active scope     → record SQL, parameters, duration
                │
                ▼
         QueryMetrics + budget evaluation
                │
                ├─ MeasureAsync → return metrics
                └─ AssertAsync  → throw QueryBudgetExceededException
```

Timing uses EF Core command-end durations (`CommandExecutedEventData.Duration`), not wall-clock guesses around the interceptor. See [docs/timing.md](docs/timing.md).

---

## Replacing a piece

`QueryBudget` is a shortcut over a default `QueryBudgetRunner`. Build a runner of your own to swap
any of the three abstractions:

```csharp
var runner = new QueryBudgetRunner(
    new MyAnalysisFactory(),        // ISqlNormalizer + IQueryFingerprinter
    new MyReportFormatter());       // IQueryReportFormatter

await runner.AssertAsync(new QueryBudgetOptions { MaxQueries = 5 }, async () => ...);
```

The normalizer and the fingerprinter come from an `IQueryAnalysisFactory` rather than being passed
directly, because the normalization mode arrives with the budget, per assertion, while the pipeline
has to be built before the queries are grouped:

```csharp
public interface IQueryAnalysisFactory
{
    ISqlNormalizer CreateNormalizer(SqlNormalizationMode mode);
    IQueryFingerprinter CreateFingerprinter(SqlNormalizationMode mode);
}
```

Start from `DefaultQueryAnalysisFactory` and override what you need. One rule matters if you write
your own fingerprinter: keep literals out of the **structural** fingerprint only. The exact one has
to keep them, or two queries differing in a value get reported as the same query.

---

## Environment guidance

| Environment | Recommendation |
|---|---|
| Test | Enabled |
| Development | Optional diagnostics |
| Production | Disabled by default |

Query Budget is designed for automated tests and development diagnostics. Production is not blocked technically, but continuous production enforcement is out of scope for 0.1.0.

Set `QueryBudgetLibraryOptions.Enabled = false` when you want the interceptor registered but inert.

---

## What 0.1.0 includes

- EF Core command capture via `DbCommandInterceptor`
- Isolated `AsyncLocal` scopes (nested scopes rejected)
- Query count, redundant executions, repeated patterns, slow queries, durations
- Configurable budgets and actionable exception messages
- `AssertAsync` and `MeasureAsync`, with a `CancellationToken` and a value-returning `AssertAsync<T>`
- Literal masking for raw SQL, so patterns are found where the provider inlines constants
- Bounded retention that never shrinks the numbers a budget is judged on
- Replaceable normalizer, fingerprinter and report formatter through `QueryBudgetRunner`
- ASP.NET Core + PostgreSQL sample
- Unit, concurrency, and Testcontainers integration tests

## What 0.1.0 does not include

Dashboards, SQL Server matrix, Dapper/NHibernate adapters, OpenTelemetry products, automatic index advice, `EXPLAIN ANALYZE`, LINQ rewriting, AI suggestions, CPU/memory profiling, or a production APM.

---

## Documentation

- [Query fingerprints](docs/query-fingerprints.md)
- [Possible N+1](docs/possible-n-plus-one.md)
- [Timing](docs/timing.md)
- [Parameter security](docs/parameter-security.md)
- [Concurrency](docs/concurrency.md)
- [Initial issues](docs/initial-issues.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Changelog](CHANGELOG.md)

---

## License

MIT © Erick Morales
