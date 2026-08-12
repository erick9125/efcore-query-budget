# Parameter security

Default display mode: `Hidden`.

| Mode | Behavior |
|---|---|
| `Hidden` | Counts only (`Distinct variants`) |
| `TypesOnly` | Parameter names and CLR types |
| `Full` | Values (local diagnostics only) |

Binary values are fingerprinted by SHA-256 length-aware hashes and never dumped into reports.
Connection strings and passwords are never written by this library.
