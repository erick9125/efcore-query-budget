# Parameter security

Default display mode: `Hidden`.

| Mode | Behavior |
|---|---|
| `Hidden` | Counts only (`Distinct variants`) |
| `TypesOnly` | Parameter names and CLR types |
| `Full` | Values (local diagnostics only) |

Binary values are fingerprinted by SHA-256 length-aware hashes and never dumped into reports.
Connection strings and passwords are never written by this library.

## What is retained

Parameter values are projected at the moment of capture, not held by reference. The command's own
value is released as soon as it returns; what the scope keeps is a `ParameterSnapshot`.

| Value | Retained |
|---|---|
| `null`, `DBNull` | `null` |
| Immutable scalars — `int`, `bool`, `decimal`, `Guid`, `DateTime`, enums… | the value |
| `string` up to 256 chars | the value |
| `string` beyond 256 chars | the first 256 chars, plus a SHA-256 of the whole |
| `byte[]` | length and a SHA-256 — never the bytes |
| Arrays | rendered elements up to 256 chars, plus a SHA-256 of all of them |
| Anything else | invariant `ToString` up to 256 chars, plus a SHA-256 of the whole |

The hash is what keeps two different values that share a prefix from being reported as the same
query. It is a fingerprint, not a way back to the value.

Projecting also makes capture correct, not only smaller: a mutable value held by reference could
change between the command running and the report being built, and the query would land in a
different group than the one it actually belonged to.
