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

Inspect the `.nupkg` before publishing. It must contain, for **every** target framework, the
assembly *and* its XML documentation file, plus a matching `.pdb` in the `.snupkg`:

```bash
unzip -l artifacts/*.nupkg    # lib/net8.0 and lib/net9.0, each with .dll + .xml
unzip -l artifacts/*.snupkg   # one .pdb per target framework
```

## Guidelines

- Keep budget evaluation pure and free of EF Core dependencies. The `Abstractions/`, `Analysis/`,
  `Budget/`, `Models/` and `Reporting/` folders must not reference EF Core types; only
  `Interceptors/` and `DependencyInjection/` may.
- Do not hash or normalize queries when no measurement scope is active.
- Prefer conservative SQL normalization over aggressive rewriting.
- Never print parameter values in reports by default.
- Keep the public API small and test-focused for 0.1.x.
