# Initial issues

## Features

- feat: implement AsyncLocal query measurement scopes
- feat: capture EF Core commands with DbCommandInterceptor
- feat: calculate query count metrics
- feat: detect exact duplicate queries
- feat: detect repeated query patterns
- feat: detect slow database commands
- feat: enforce configurable query budgets
- feat: add actionable exception formatting

## Tests

- test: verify concurrent scope isolation
- test: add PostgreSQL integration tests with Testcontainers
- test: add ASP.NET Core WebApplicationFactory example

## Docs

- docs: explain exact duplicates vs repeated patterns
- docs: document query parameter security
- docs: explain timing limitations
