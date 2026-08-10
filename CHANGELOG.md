# Changelog

## 0.1.0

- Capture EF Core database commands inside isolated test scopes.
- Enforce configurable budgets for query count, exact duplicates, repeated patterns, slow queries, and total database time.
- Support `AssertAsync` and `MeasureAsync`.
- Integrate through `DbCommandInterceptor` and `AddEfCoreQueryBudget`.
- Include ASP.NET Core + PostgreSQL sample and Testcontainers-backed integration tests.
