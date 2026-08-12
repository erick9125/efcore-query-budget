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
dotnet pack src/EfCoreQueryBudget/EfCoreQueryBudget.csproj -c Release -o artifacts
```

Inspect the `.nupkg` before publishing. It must contain, for **every** target framework, the
assembly *and* its XML documentation file, plus a matching `.pdb` in the `.snupkg`:

```bash
unzip -l artifacts/*.nupkg    # lib/net8.0 and lib/net9.0, each with .dll + .xml
unzip -l artifacts/*.snupkg   # one .pdb per target framework
```

## Release

The version comes from the git tag, not from a property in a `.csproj`. MinVer derives it, so the
package can never disagree with the commit it was built from. Untagged builds are
`0.0.0-alpha.0.<commit height>`.

```bash
git tag v1.2.3
git push origin v1.2.3
```

That pushes the tag, which triggers `.github/workflows/release.yml`: build, all three test suites,
pack, a check that every target framework ships its assembly, XML docs and symbols, then
`dotnet nuget push` and a GitHub release. It needs a `NUGET_API_KEY` repository secret.

Use a prerelease tag (`v1.2.3-rc.1`) to publish a prerelease; NuGet infers that from the version.

## Guidelines

- Keep budget evaluation pure and free of EF Core dependencies. The `Abstractions/`, `Analysis/`,
  `Budget/`, `Models/` and `Reporting/` folders must not reference EF Core types; only
  `Interceptors/` and `DependencyInjection/` may.
- Do not hash or normalize queries when no measurement scope is active.
- Prefer conservative SQL normalization over aggressive rewriting.
- Never print parameter values in reports by default.
- Keep the public API small and test-focused for 0.1.x.
