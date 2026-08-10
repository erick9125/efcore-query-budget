# Contributing

## Development

```bash
dotnet restore
dotnet build
dotnet test
```

Integration tests require Docker for Testcontainers PostgreSQL.

## Pack

```bash
dotnet pack src/QueryBudget.EntityFrameworkCore/QueryBudget.EntityFrameworkCore.csproj -c Release -o artifacts
```

Inspect the `.nupkg` before publishing. The package includes both the main assembly and `QueryBudget.Core`.

## Guidelines

- Keep budget evaluation pure and free of EF Core dependencies.
- Do not hash or normalize queries when no measurement scope is active.
- Prefer conservative SQL normalization over aggressive rewriting.
- Never print parameter values in reports by default.
- Keep the public API small and test-focused for 0.1.x.
