# Concurrency

Scopes are isolated with `AsyncLocal<QueryScope>`.

```csharp
var results = await Task.WhenAll(
    QueryBudget.MeasureAsync(() => ServiceA.RunAsync()),
    QueryBudget.MeasureAsync(() => ServiceB.RunAsync()));
```

Each scope must receive only its own queries.

## Nested scopes

Nested scopes throw in 0.1.0.

## WebApplicationFactory

HTTP requests often execute outside the test method's AsyncLocal flow. When AsyncLocal is empty and exactly one budget scope is active, commands are attributed to that sole scope. This enables endpoint budget tests without middleware.

With multiple concurrent active scopes and no AsyncLocal flow, commands are not assigned to a random scope.
