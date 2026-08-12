# Changelog

## Unreleased

### Added

- `QueryBudgetRunner`, an instantiable composition root, and `IQueryAnalysisFactory`. The three
  abstractions the library declares — `ISqlNormalizer`, `IQueryFingerprinter` and
  `IQueryReportFormatter` — existed as interfaces but there was no way to supply your own through
  the public API: the static facade fixed its collaborators in static fields and four classes
  resolved their own dependencies with `?? new DefaultSqlNormalizer()`. `QueryBudget` is now a
  shortcut over a default runner, so nothing changes for callers that do not need to replace a piece.
- The interception path is now covered by tests, and covered on both target frameworks. All eight
  recording entry points — reader, scalar and non-query, sync and async, plus the two failure
  callbacks — had no direct test, and the only indirect coverage lived in a `net9.0`-only project,
  so the `net8.0` assembly shipped without ever having executed a capture in any test.
- `QueryBudgetOptions.MaxExecutionsPerPattern` bounds the largest repeated pattern, reported as
  `QueryMetrics.MaximumPatternExecutions`. `MaxRepeatedPatterns` counts groups, so five executions
  in one place and five thousand in another both counted as one; the size of an N+1, which is what
  it costs, could not be limited at all.
- `QueryGroup.Operation` says whether a group reads, writes, or does neither.
- `QueryBudgetOptions.MaxRecordedQueries` caps how many queries a scope retains for analysis
  (default `10_000`, `null` for no limit). A long-running scope no longer grows without bound.
  Retention is never cut silently: the number dropped is reported as
  `QueryMetrics.DiscardedQueryCount` and warned about in the report. Only retention is capped —
  `QueryCount`, `TotalDuration`, `MaximumDuration` and `SlowQueryCount` still cover every command
  the scope accepted, so a budget cannot pass because the scope stopped retaining.
- `QueryBudgetOptions.SqlNormalization` selects how SQL is normalized before queries are grouped
  into patterns. The new `SqlNormalizationMode.MaskLiterals` replaces inline string and numeric
  literals with `?` and collapses variable-length `IN` lists, so raw SQL, `FromSqlRaw` and
  provider-inlined constants can be grouped at all. It leaves parameters, quoted identifiers,
  `NULL` and comments alone, and applies only to the structural fingerprint — exact-duplicate
  detection never masks, so two queries differing in a value stay two queries. The default,
  `WhitespaceOnly`, is the previous behavior.

### Fixed

- Metrics can no longer be scored against a budget that did not produce them. `Calculate` and
  `Evaluate` each took their own `QueryBudgetOptions`, so `result.Budget` could report one budget
  while the numbers were computed under another — the slow-query threshold, the repeat threshold and
  the normalization mode all shape the metrics. The budget now travels on the metrics.
- Each query is fingerprinted once per measurement instead of twice. The calculator composed two
  detectors with two fingerprinters, and both ask for the exact fingerprint.
- A bulk insert is no longer reported as a possible N+1. `SaveChanges` over 50 new entities emits 50
  executions of one `INSERT` shape with different values, which is the exact signature the pattern
  detector looked for, so any test writing more than a handful of rows triggered the library's
  headline warning on normal work.
- Parameter values are no longer held by reference for the life of the scope. They are projected at
  capture time into an immutable `ParameterSnapshot`, so a large payload is not pinned, personal
  data does not outlive the command that used it, and a mutable value can no longer change between
  the command running and the fingerprint being computed — which used to move an already-executed
  query into a different group. Binary payloads keep only their length and a SHA-256; long strings
  and arrays keep a bounded rendering plus a hash of the whole, so two values sharing a prefix are
  still told apart.
- A repeated pattern in SQL carrying inline literals is now detectable. Two things hid it: the
  normalizer gave every execution a different structural fingerprint, and the pattern filter
  required more than one distinct *parameter set* — of which a query with no parameters has exactly
  one. Fixing only the first would have changed nothing observable.
- Commands are no longer attributed to a scope on another execution flow. The previous behavior
  claimed any flow-less command whenever exactly one scope was active process-wide, so a parallel
  test, a hosted service or a background seed would be counted against the active budget. It is
  now opt-in as `ScopeAttributionMode.SingleActiveScopeFallback`; the default is `AsyncLocalOnly`.
  For `WebApplicationFactory`, set `factory.Server.PreserveExecutionContext = true` before creating
  the client.
- A second capture of the same command execution is discarded instead of doubling every metric.
  EF Core invokes an interceptor once per attachment, and a test host that re-registers
  `AddDbContext` on top of the application's own ends up attaching it twice. The discarded count
  is reported as `QueryMetrics.DuplicateCaptureCount` and warned about in the report.

### Breaking

- `QueryBudgetEvaluator.Evaluate` takes only the metrics; the budget comes from the new required
  `QueryMetrics.Budget`. `QueryBudgetResult.Budget` is now derived from the metrics rather than
  stored, so the two cannot disagree.
- `DefaultQueryFingerprinter`, `ExactDuplicateDetector`, `RepeatedPatternDetector` and
  `QueryMetricsCalculator` take their dependencies explicitly instead of defaulting them, and the
  calculator composes from an `IQueryAnalysisFactory` rather than accepting pre-built detectors.
- `QueryBudgetContext.Record` is internal. Capture is the interceptor's job; a public entry point
  was mutable global state offered as API.
- `ExactDuplicateCount` and `RepeatedPatternCount` count reads only. Repeating a read with the same
  parameters is provably redundant; repeating a write is not, so the library no longer claims it is.
  Writes and commands that are neither still appear in `ExactDuplicateGroups` and
  `RepeatedPatternGroups`, tagged with their `Operation`, and in the report under their own heading.
- Captured parameter values may now arrive as `ParameterSnapshot` rather than as the original
  object. Code reading `RecordedQuery.Parameters` from a captured command should handle it; values
  put there by hand are untouched, and immutable scalars and short strings are still stored as
  themselves.
- `QueryMetricsCalculator.Calculate` takes an optional third argument, `QueryCaptureTotals`. Omit it
  and the aggregates are derived from the queries exactly as before.
- `QueryGroup.DistinctParameterSetCount` is now `QueryGroup.DistinctVariantCount`, and it counts
  distinct exact fingerprints rather than distinct parameter sets. For a parameterized query the
  number is the same as before; for one with inline literals it is the only thing that can tell the
  executions apart. Reports say `Distinct variants: N` instead of `Distinct parameter sets: N`.
- Removed `DefaultQueryFingerprinter.ParameterSetKey`. Nothing calls it now that variants are
  counted by exact fingerprint, and it was the one place bypassing the `IQueryFingerprinter`
  abstraction.
- `DefaultQueryFingerprinter` takes a second normalizer, `exactNormalizer`, so the structural and
  exact fingerprints can normalize differently. Both parameters are optional and default to the
  previous behavior.
- Renamed the package, assembly and root namespace, aligning with the already-published
  `erick9125.AuditableOperations`. Nothing was released under the old identity, so no consumer is
  affected.

  | | Before | After |
  |---|---|---|
  | Package | `ErickMorales.EntityFrameworkCore.QueryBudget` | `erick9125.EfCoreQueryBudget` |
  | Assembly, namespace | `ErickMorales.EntityFrameworkCore.QueryBudget` | `EfCoreQueryBudget` |

  This also resolves the type↔namespace collision flagged by CA1724: the `QueryBudget` facade no
  longer sits inside a namespace of the same name, so call sites keep reading
  `QueryBudget.AssertAsync(...)` while only the `using` changes.

- Removed `QueryBudgetLibraryOptions.SlowQueryThreshold` and
  `QueryBudgetLibraryOptions.ParameterDisplayMode`. Neither was ever read: the values were
  silently discarded while the README and the sample taught you to set them. Both settings
  already exist on `QueryBudgetOptions`, where they do take effect — move them there.
  `AddEfCoreQueryBudget` now configures capture only, via `Enabled`.

### Changed

- The package version now comes from the git tag via MinVer instead of a hardcoded `<Version>`,
  and a tag push publishes to NuGet through the new release workflow.
- CI checks formatting, runs the unit suite against both target frameworks, and fails if a packed
  target framework is missing its assembly, XML documentation or symbols.
- The package now targets `net8.0` and `net9.0` instead of `net9.0` only.
- `QueryBudget.Core` was merged into the main assembly. The package previously carried
  `Core.dll` without its XML documentation and without a PDB, so most of the public API had
  no IntelliSense and could not be stepped into. Namespaces are unchanged.

## 0.1.0

- Capture EF Core database commands inside isolated test scopes.
- Enforce configurable budgets for query count, exact duplicates, repeated patterns, slow queries, and total database time.
- Support `AssertAsync` and `MeasureAsync`.
- Integrate through `DbCommandInterceptor` and `AddEfCoreQueryBudget`.
- Include ASP.NET Core + PostgreSQL sample and Testcontainers-backed integration tests.
