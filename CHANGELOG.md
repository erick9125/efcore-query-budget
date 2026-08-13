# Changelog

## 0.1.0 — 2026-08-12

First release. Nothing was published before this, so there is nothing to break and no migration to
do; the entries below describe what the library does rather than how it changed.

### Added

- **Query budgets in tests.** `QueryBudget.AssertAsync` runs an action inside a measurement scope
  and throws `QueryBudgetExceededException` when a limit is exceeded, with the report as the
  message. `AssertAsync<T>` returns what the action produced, and `MeasureAsync` measures without
  throwing. All of them take an optional `CancellationToken`.
- **Limits**, all optional: `MaxQueries`, `MaxExactDuplicates`, `MaxRepeatedPatterns`,
  `MaxExecutionsPerPattern`, `MaxSlowQueries`, `MaxTotalDuration` and `MaxSingleQueryDuration`.
  `MaxRepeatedPatterns` bounds how many places repeat; `MaxExecutionsPerPattern` bounds how big the
  worst one is.
- **Capture** through a `DbCommandInterceptor`, registered with `AddEfCoreQueryBudget`. When no
  scope is active the interceptor returns immediately.
- **Detection** of exact duplicates and of repeated patterns, the latter reported as a *possible*
  N+1 — never as a confirmed one.
- **Reports** that name the limit, the budget and the actual value, and show the offending query
  groups. Parameter values are hidden by default.
- **Extensibility.** `QueryBudgetRunner` is the composition root; supply your own
  `IQueryAnalysisFactory` to replace the `ISqlNormalizer` or the `IQueryFingerprinter`, or your own
  `IQueryReportFormatter` to change the report. `QueryBudget` is a shortcut over a default runner.
- Targets `net8.0` and `net9.0`, each built and tested against its own EF Core major. An ASP.NET
  Core + PostgreSQL sample and Testcontainers-backed integration tests ship with the repository.

### Behavior worth knowing

These are the decisions most likely to surprise you. Each is deliberate.

- **Attribution follows the execution flow.** A command is counted against the scope on its own
  async flow, and commands outside any scope are ignored, so a parallel test or a hosted service
  cannot land in your budget. For `WebApplicationFactory`, set
  `factory.Server.PreserveExecutionContext = true` before creating the client, or `TestServer` will
  run the request outside your scope and the budget will see nothing.
  `ScopeAttributionMode.SingleActiveScopeFallback` relaxes this and is opt-in, because it cannot
  tell your command from anyone else's.
- **Duplicates and patterns cover reads only.** Running the same `SELECT` with the same parameters
  twice returns the same rows, so the second execution is provably wasted. Running the same `INSERT`
  twice is not: it adds two rows. A `SaveChanges` over 50 new entities emits 50 executions of one
  `INSERT` shape — the exact signature of an N+1, and normal work. Writes are still detected and
  reported, under a heading that does not call them a defect, and every `QueryGroup` carries its
  `Operation`.
- **Inline literals need `SqlNormalization = MaskLiterals`.** Patterns group by normalized SQL, and
  the default only collapses whitespace, so raw SQL or `FromSqlRaw` gives every execution its own
  fingerprint and no pattern is found. Masking applies to the structural fingerprint only — the
  exact one keeps literals, or two queries differing in a value would be called the same query. The
  trade-off is that meaningful literals collapse too, so `LIMIT 10` and `LIMIT 20` become one
  pattern.
- **Retention is capped, counting is not.** `MaxRecordedQueries` (default `10_000`) bounds how many
  queries a scope holds. Past it, commands are still counted and timed, so `QueryCount`,
  `TotalDuration`, `MaximumDuration` and `SlowQueryCount` always cover everything that ran and a
  budget can never pass because the scope stopped looking. The number dropped is reported as
  `DiscardedQueryCount`; only the duplicate and pattern groups cover the retained sample.
- **Parameter values are projected, not retained.** Capture stores an immutable `ParameterSnapshot`
  rather than the caller's object, so a large payload is not pinned and personal data does not
  outlive the command. Binary payloads keep only their length and a hash; long strings and arrays
  keep a bounded rendering plus a hash of the whole, so two values sharing a prefix are still told
  apart. Reading `RecordedQuery.Parameters` from a captured command should expect it; values you put
  there yourself are untouched.
- **A repeat capture of one execution is discarded.** EF Core invokes an interceptor once per
  attachment, so a test host that re-registers `AddDbContext` on top of the application's own would
  otherwise double every metric. The count surfaces as `DuplicateCaptureCount` and as a warning; a
  non-zero value means the registration needs fixing.
- **Timing** comes from EF Core's command-end durations, not from a clock around the interceptor.

### Notes

- The package version is derived from the git tag through MinVer; a tag push publishes to NuGet.
- The public surface is tracked in `PublicAPI.Shipped.txt`, so any change to it appears as a diff in
  the pull request.
