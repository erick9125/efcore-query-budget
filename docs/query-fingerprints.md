# Query fingerprints

Two fingerprints are used:

## Structural

`SHA256(normalized SQL)`

Groups the same SQL shape regardless of parameter values. Used for repeated-pattern / possible N+1 detection.

## Exact

`SHA256(normalized SQL + deterministic parameters)`

Groups identical SQL with identical parameter values. Used for exact-duplicate detection.

## Normalization (0.1.0)

Only trim and collapse whitespace. No predicate rewriting, alias normalization, or ORDER BY removal.
