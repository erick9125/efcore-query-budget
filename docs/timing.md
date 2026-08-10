# Timing

EF Core Query Budget records duration from EF Core command end events (`CommandExecutedEventData.Duration` / `CommandErrorEventData.Duration`).

Notes:

- Prefer EF-provided durations over wall-clock timestamps around the interceptor.
- Durations include database round-trip time as observed by EF Core, not application CPU time.
- Slow-query thresholds are approximate diagnostics, not SLAs.
- Total duration is the sum of recorded command durations inside the scope.
