# Changelog

## Unreleased

### Added

- `QueryBudgetOptions.SqlNormalization` selects how SQL is normalized before queries are grouped
  into patterns. The new `SqlNormalizationMode.MaskLiterals` replaces inline string and numeric
  literals with `?` and collapses variable-length `IN` lists, so raw SQL, `FromSqlRaw` and
  provider-inlined constants can be grouped at all. It leaves parameters, quoted identifiers,
  `NULL` and comments alone, and applies only to the structural fingerprint — exact-duplicate
  detection never masks, so two queries differing in a value stay two queries. The default,
  `WhitespaceOnly`, is the previous behavior.

### Fixed

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
