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

## Reads only

Both signals apply to reads. The reasoning behind them does not survive the move to a write:

| | Same SQL, same parameters, twice |
|---|---|
| `SELECT` | Same rows both times, so the second execution is provably wasted |
| `INSERT` | Two rows. Both intended |
| `UPDATE counters SET n = n + 1` | Counted twice. Intended |

The pattern signal fails the same way, and more often: `SaveChanges` over 50 new entities emits 50
executions of one `INSERT` shape with different values — indistinguishable from an N+1 by shape
alone, and a perfectly normal bulk insert.

So writes, and commands that are neither (session settings, transaction control, DDL, sequence
calls), are excluded from `RedundantExecutionCount` and `RepeatedPatternCount`. They are still grouped
and still shown in the report, under a heading that does not claim they are a problem. The
classification is on `QueryGroup.Operation`.

## How many places, how big

`MaxRepeatedPatterns` counts groups: five executions in one place and five thousand in another both
count as one. `MaxExecutionsPerPattern` bounds the largest group instead, which is the number that
tracks how much the N+1 actually costs. They answer different questions and are usually set together.
