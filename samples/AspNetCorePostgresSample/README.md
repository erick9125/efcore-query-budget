# AspNetCorePostgresSample

Minimal ASP.NET Core + EF Core + PostgreSQL sample for EF Core Query Budget.

## Endpoints

- `GET /api/posts/problematic` — loads posts then authors one-by-one (N+1 style)
- `GET /api/posts/optimized` — loads posts with `Include(x => x.Author)`

## Run locally

Requires PostgreSQL.

```bash
dotnet run --project samples/AspNetCorePostgresSample
```

## Tests

Integration tests use Testcontainers and live under `tests/QueryBudget.IntegrationTests`.
Docker must be running.
