# Query fingerprints

Two fingerprints are used:

## Structural

`SHA256(normalized SQL)`

Groups the same SQL shape regardless of parameter values. Used for repeated-pattern / possible N+1 detection.

## Exact

`SHA256(normalized SQL + deterministic parameters)`

Groups identical SQL with identical parameter values. Used for exact-duplicate detection.

## Normalization

Set by `QueryBudgetOptions.SqlNormalization`. It applies to the **structural** fingerprint only —
the exact fingerprint always keeps literals, or two queries differing in a value would be reported
as the same query.

### `WhitespaceOnly` (default)

Trim and collapse whitespace. No predicate rewriting, alias normalization, or ORDER BY removal.

Note the consequence: if the SQL carries inline literals rather than parameters — raw SQL,
`FromSqlRaw`, or constants the provider inlines — every execution gets a different structural
fingerprint, lands in its own group of one, and **no repeated pattern is ever detected**. Parameterized
queries are unaffected, since their SQL is identical across executions.

### `MaskLiterals`

Also replaces inline literals with `?` and collapses variable-length `IN` lists, so those executions
group into one pattern.

| Input | Result |
|---|---|
| `'text'`, `'it''s'`, `N'text'`, `E'text'`, `$$text$$` | `?` |
| `10`, `1.5`, `.5`, `1e-5`, `0x1F` | `?` |
| `IN (1, 2, 3)`, `IN (@p0, @p1)` | `IN (?)` |
| `@p0`, `@__id_0`, `:id`, `$1` | untouched |
| `"Order"`, `[Order]`, `` `order` ``, `table1` | untouched |
| `NULL`, `TRUE`, `FALSE` | untouched |
| `-- tag`, `/* tag */` | untouched |

Two deliberate limits:

- A literal that carries meaning collapses too, so `LIMIT 10` and `LIMIT 20` become one pattern.
- `VALUES (1, 2)` is **not** collapsed the way `IN` is: a different column count is a different
  query shape.

## Distinct variants

Within a pattern group, `QueryGroup.DistinctVariantCount` counts distinct **exact** fingerprints, not
distinct parameter sets. A group of many executions with more than one variant is the N+1 shape; a
group with a single variant is the same query run repeatedly, which is an exact duplicate instead.

Counting exact fingerprints rather than parameter sets is what makes the masked mode work: a query
with inline literals has no parameters at all, so a parameter-set count would be 1 for every raw-SQL
group and discard it.
