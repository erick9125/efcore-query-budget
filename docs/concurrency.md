# Concurrency

Scopes are isolated with `AsyncLocal<QueryScope>`. A command is attributed to the scope on its own
execution flow, and commands that run outside any scope are ignored.

```csharp
var results = await Task.WhenAll(
    QueryBudget.MeasureAsync(() => ServiceA.RunAsync()),
    QueryBudget.MeasureAsync(() => ServiceB.RunAsync()));
```

Each scope receives only its own queries, including when unrelated database work runs concurrently.

## Nested scopes

Nested scopes throw.

## WebApplicationFactory and TestServer

`TestServer` does **not** flow the caller's execution context into the request pipeline by default,
so a budget wrapped around an HTTP call would see zero queries. Turn the flow on before creating
the client:

```csharp
var factory = new AppFactory();
factory.Server.PreserveExecutionContext = true;   // must be set before CreateClient()
var client = factory.CreateClient();
```

With that one line, endpoint budgets work under the default attribution mode and stay correct when
tests run in parallel.

## Attribution modes

| Mode | Behavior |
|---|---|
| `AsyncLocalOnly` (default) | Follow the execution flow. Ignore anything outside a scope. |
| `SingleActiveScopeFallback` | Also claim flow-less commands when exactly one scope is active process-wide. |

```csharp
services.AddEfCoreQueryBudget(options =>
{
    options.AttributionMode = ScopeAttributionMode.SingleActiveScopeFallback;
});
```

The fallback exists for hosts where the execution context genuinely cannot be made to flow. It is
opt-in because it cannot distinguish a command issued by the code under measurement from one
issued by anything else in the process: a parallel test, a hosted service or a background seed
will all be counted against the active budget. Prefer `PreserveExecutionContext`.

## Attaching the interceptor more than once

EF Core will invoke the same interceptor instance once per attachment, which would multiply every
metric. This most often happens in a test host that calls `AddDbContext` again on top of the
application's own registration — removing `DbContextOptions<T>` does not undo the original options
callback. Point the existing registration at the test database instead:

```csharp
builder.UseSetting("ConnectionStrings:Default", container.GetConnectionString());
```

As a safety net, repeat captures of the same execution are discarded — EF Core issues one
`CommandId` per execution — and the count surfaces as `QueryMetrics.DuplicateCaptureCount` and as a
warning in the report. A non-zero value means the registration still needs fixing.
