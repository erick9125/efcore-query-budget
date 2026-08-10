# Changelog

## Unreleased

### Breaking

- Removed `QueryBudgetLibraryOptions.SlowQueryThreshold` and
  `QueryBudgetLibraryOptions.ParameterDisplayMode`. Neither was ever read: the values were
  silently discarded while the README and the sample taught you to set them. Both settings
  already exist on `QueryBudgetOptions`, where they do take effect — move them there.
  `AddEfCoreQueryBudget` now configures capture only, via `Enabled`.

### Changed

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
