# Possible N+1 patterns

Exact duplicates and repeated patterns are different problems.

## Exact duplicate

```sql
SELECT ... FROM users WHERE id = @__id_0
```

with `@__id_0 = 10` repeated many times.

## Repeated pattern / possible N+1

Same SQL text, different parameter values:

```text
10
11
12
13
14
```

Report wording:

```text
Possible N+1 query pattern

Executions: 15
Distinct variants: 15
```

Never claim `N+1 confirmed`. The signal is strong enough to investigate, not strong enough to prove.
