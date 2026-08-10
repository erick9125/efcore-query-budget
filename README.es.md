# EF Core Query Budget

[![CI](https://img.shields.io/badge/ci-GitHub%20Actions-blue)](.github/workflows/ci.yml)
[![NuGet](https://img.shields.io/badge/nuget-ErickMorales.EntityFrameworkCore.QueryBudget-blue)](https://www.nuget.org/packages/ErickMorales.EntityFrameworkCore.QueryBudget)
[![Target](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

Define y aplica **presupuestos de consultas de base de datos** en tus tests de EF Core.

Captura comandos SQL dentro de scopes de ejecución aislados y falla en cuanto un endpoint, servicio o repositorio empeora en silencio: demasiadas consultas, duplicados exactos, patrones repetidos o trabajo lento en base de datos — antes de que ese coste llegue a producción.

> **English docs:** [README.md](README.md)

---

## Promesa (0.1.0)

> Capturar comandos de base de datos de EF Core dentro de scopes de test aislados y aplicar presupuestos configurables de conteo, duplicados exactos, patrones repetidos, consultas lentas y tiempo total de base de datos.

---

## El problema

Los tests funcionales suelen demostrar que una API sigue devolviendo `200 OK`. Casi nunca demuestran que el trabajo de base de datos detrás de esa respuesta se mantuvo barato.

| | Antes | Después de un cambio |
|---|---|---|
| `GET /orders` | 3 consultas · 35 ms | 31 consultas · 280 ms |
| Resultado | Los tests pasan | Los tests siguen pasando |

La comodidad del ORM oculta patrones costosos:

- un `Include` quitado “temporalmente”
- un bucle que carga filas relacionadas una a una
- la misma consulta repetida con parámetros idénticos
- round trips lentos que solo aparecen con datos reales

**EF Core Query Budget** convierte esa regresión silenciosa en una condición verificable.

```text
EF Core query budget exceeded

Scope: GET /api/orders

Query count
  Budget: <= 5
  Actual:   31

Exact duplicates
  Budget: <= 0
  Actual:   4

Repeated query patterns
  Budget: <= 1
  Actual:   1

Possible N+1 query pattern.
```

---

## Para qué sirve

Úsalo cuando quieras que el rendimiento de base de datos forme parte del contrato de tus tests:

| Caso de uso | Ejemplo |
|---|---|
| Tests de servicio / caso de uso | Asegurar que un use case se mantiene dentro de un presupuesto |
| Tests de repositorio | Detectar fan-out accidental en el acceso a datos |
| Tests de integración | Medir SQL real de EF Core contra PostgreSQL |
| Tests de endpoint | Envolver llamadas HTTP de `WebApplicationFactory` |

**No** es un APM, un dashboard ni un profiler de producción. Es una herramienta enfocada en testing y diagnóstico para EF Core.

---

## Características

| Característica | Comportamiento |
|---|---|
| Captura de comandos | `DbCommandInterceptor` para Reader / Scalar / NonQuery (sync + async) |
| Scopes aislados | Scopes de medición con `AsyncLocal` por flujo de ejecución |
| Conteo de consultas | Total de comandos atribuidos al scope activo |
| Duplicados exactos | Mismo SQL + mismos valores de parámetros, repetidos |
| Patrones repetidos | Mismo SQL + distintos conjuntos de parámetros (posible N+1) |
| Consultas lentas | Cuenta comandos en o por encima de un umbral de duración |
| Presupuestos de duración | Tiempo total de BD y peor consulta individual |
| Assert o medir | Fallar el test, o solo recolectar métricas |
| Reportes seguros | Valores de parámetros ocultos por defecto |
| Listo para ASP.NET Core | Funciona con DI + `WebApplicationFactory` |

---

## Instalación

```bash
dotnet add package ErickMorales.EntityFrameworkCore.QueryBudget
```

**Requisitos:** .NET 8 o .NET 9, con la major de EF Core correspondiente (8.x o 9.x). ASP.NET Core es opcional: la librería funciona en tests de servicios y repositorios sin host web.

---

## Inicio rápido

### 1. Registrar el interceptor

```csharp
using ErickMorales.EntityFrameworkCore.QueryBudget;
using Microsoft.EntityFrameworkCore;

builder.Services.AddEfCoreQueryBudget();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddInterceptors(
            serviceProvider.GetRequiredService<QueryBudgetCommandInterceptor>());
});
```

El registro solo controla la **captura**. Los umbrales y límites viven en `QueryBudgetOptions` y
se pasan en cada aserción, porque dos presupuestos de una misma suite rara vez quieren los mismos
números. Aquí la única palanca es `Enabled`:

```csharp
builder.Services.AddEfCoreQueryBudget(options =>
{
    options.Enabled = !builder.Environment.IsProduction();
});
```

### 2. Afirmar un presupuesto en un test

```csharp
using ErickMorales.EntityFrameworkCore.QueryBudget;

await QueryBudget.AssertAsync(
    new QueryBudgetOptions
    {
        MaxQueries = 5,
        MaxExactDuplicates = 0,
        ScopeLabel = "GET /api/orders"
    },
    async () =>
    {
        await client.GetAsync("/api/orders");
    });
```

---

## Ejemplos de uso

### Assert sobre un servicio

```csharp
await QueryBudget.AssertAsync(
    new QueryBudgetOptions
    {
        MaxQueries = 5,
        MaxExactDuplicates = 0,
        MaxRepeatedPatterns = 1,
        MaxTotalDuration = TimeSpan.FromMilliseconds(150),
        ScopeLabel = "OrderService.GetOrdersAsync"
    },
    async () =>
    {
        await orderService.GetOrdersAsync();
    });
```

### Medir sin fallar

Útil para establecer una línea base o depurar un hot path:

```csharp
var measurement = await QueryBudget.MeasureAsync(async () =>
{
    await orderService.GetOrdersAsync();
});

Console.WriteLine(measurement.Metrics.QueryCount);
Console.WriteLine(measurement.Metrics.ExactDuplicateCount);
Console.WriteLine(measurement.Metrics.RepeatedPatternCount);
Console.WriteLine(measurement.Metrics.TotalDuration);
```

### Test HTTP con WebApplicationFactory

```csharp
public class OrdersTests
{
    private readonly HttpClient _client;

    public OrdersTests(AppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Orders_endpoint_stays_within_budget()
    {
        await QueryBudget.AssertAsync(
            new QueryBudgetOptions
            {
                MaxQueries = 4,
                MaxExactDuplicates = 0,
                ScopeLabel = "GET /api/orders"
            },
            async () =>
            {
                var response = await _client.GetAsync("/api/orders");
                response.EnsureSuccessStatusCode();
            });
    }
}
```

Cuando la petición HTTP se ejecuta fuera del flujo `AsyncLocal` del test y hay exactamente un scope de presupuesto activo, los comandos se atribuyen a ese único scope. Ver [docs/concurrency.md](docs/concurrency.md).

### Detectar un posible N+1

```csharp
// Problemático: 1 consulta de posts + N consultas de authors
var posts = await context.Posts.ToListAsync();
foreach (var post in posts)
{
    post.Author = await context.Authors
        .SingleAsync(x => x.Id == post.AuthorId);
}

// Optimizado: 1 consulta
var posts = await context.Posts
    .Include(x => x.Author)
    .ToListAsync();
```

Un presupuesto como `MaxQueries = 4` / `MaxRepeatedPatterns = 0` falla en la ruta problemática y pasa en la optimizada. La app de ejemplo en `samples/AspNetCorePostgresSample` demuestra ambos endpoints.

---

## Opciones de presupuesto

Todos los límites son opcionales. Configura solo lo que quieras forzar.

```csharp
new QueryBudgetOptions
{
    MaxQueries = 5,
    MaxExactDuplicates = 0,
    MaxRepeatedPatterns = 1,
    MaxSlowQueries = 0,
    MaxTotalDuration = TimeSpan.FromMilliseconds(150),
    MaxSingleQueryDuration = TimeSpan.FromMilliseconds(80),
    SlowQueryThreshold = TimeSpan.FromMilliseconds(100),
    RepeatedPatternThreshold = 5,
    ScopeLabel = "GET /api/orders",
    ParameterDisplayMode = QueryParameterDisplayMode.Hidden
}
```

| Opción | Significado |
|---|---|
| `MaxQueries` | Máximo de comandos en el scope |
| `MaxExactDuplicates` | Máximo de ejecuciones exactas redundantes |
| `MaxRepeatedPatterns` | Máximo de grupos de patrón repetido |
| `MaxSlowQueries` | Máximo de comandos ≥ `SlowQueryThreshold` |
| `MaxTotalDuration` | Suma de duraciones de comandos |
| `MaxSingleQueryDuration` | Peor comando individual |
| `RepeatedPatternThreshold` | Ejecuciones mínimas para que un patrón cuente (por defecto `5`) |
| `ScopeLabel` | Se muestra en el reporte de fallo |

---

## Métricas

`QueryMetrics` devuelto por `MeasureAsync` / adjunto a los fallos:

| Métrica | Significado |
|---|---|
| `QueryCount` | Comandos atribuidos al scope |
| `ExactDuplicateCount` | Ejecuciones exactas redundantes |
| `RepeatedPatternCount` | Grupos de patrón repetido |
| `SlowQueryCount` | Comandos en o por encima del umbral lento |
| `TotalDuration` | Suma de duraciones de comandos |
| `MaximumDuration` | Comando individual más lento |
| `ExactDuplicateGroups` | Duplicados exactos agrupados |
| `RepeatedPatternGroups` | Patrones estructurales agrupados |

---

## Duplicados exactos vs patrones repetidos

**Duplicado exacto** — mismo SQL normalizado y mismos valores de parámetros:

```text
SELECT ... FROM users WHERE id = @__id_0
@__id_0 = 10   (repetido)
```

Suele ser trabajo desperdiciado: cachea, agrupa o deja de llamarlo dos veces.

**Patrón repetido** — misma forma de SQL, distintos conjuntos de parámetros:

```text
@__id_0 = 10
@__id_0 = 11
@__id_0 = 12
...
```

A menudo es un posible N+1. Los reportes dicen:

```text
Possible N+1 query pattern
Executions: 15
Distinct parameter sets: 15
```

Nunca `N+1 confirmed`. La señal basta para investigar, no para probar la intención. Detalles: [docs/possible-n-plus-one.md](docs/possible-n-plus-one.md).

---

## Seguridad de parámetros

Los parámetros de consulta pueden contener emails, tokens, identificadores o contraseñas.

Por defecto, los reportes solo muestran conteos:

```text
Distinct parameter sets: 12
```

| Modo | Comportamiento |
|---|---|
| `Hidden` (por defecto) | Solo conteos |
| `TypesOnly` | Nombres y tipos CLR |
| `Full` | Valores — solo diagnóstico local |

Los payloads binarios se hashean para fingerprinting y nunca se vuelcan en los reportes. Ver [docs/parameter-security.md](docs/parameter-security.md).

---

## Cómo funciona la captura

```text
Comando EF Core
      │
      ▼
QueryBudgetCommandInterceptor
      │
      ├─ sin scope activo  → retorno inmediato (overhead casi nulo)
      └─ scope activo      → registrar SQL, parámetros, duración
                │
                ▼
         QueryMetrics + evaluación del presupuesto
                │
                ├─ MeasureAsync → devolver métricas
                └─ AssertAsync  → lanzar QueryBudgetExceededException
```

El timing usa las duraciones de fin de comando de EF Core (`CommandExecutedEventData.Duration`), no relojes de pared alrededor del interceptor. Ver [docs/timing.md](docs/timing.md).

---

## Guía por entorno

| Entorno | Recomendación |
|---|---|
| Test | Habilitado |
| Development | Diagnóstico opcional |
| Production | Deshabilitado por defecto |

Query Budget está pensado para tests automatizados y diagnóstico en desarrollo. Producción no se bloquea técnicamente, pero el enforcement continuo en producción queda fuera del alcance de 0.1.0.

Usa `QueryBudgetLibraryOptions.Enabled = false` cuando quieras el interceptor registrado pero inerte.

---

## Qué incluye 0.1.0

- Captura de comandos EF Core con `DbCommandInterceptor`
- Scopes aislados con `AsyncLocal` (scopes anidados rechazados)
- Conteo, duplicados exactos, patrones repetidos, consultas lentas y duraciones
- Presupuestos configurables y mensajes de excepción accionables
- `AssertAsync` y `MeasureAsync`
- Sample ASP.NET Core + PostgreSQL
- Tests unitarios, de concurrencia e integración con Testcontainers

## Qué no incluye 0.1.0

Dashboards, matriz SQL Server, adaptadores Dapper/NHibernate, productos OpenTelemetry, consejo automático de índices, `EXPLAIN ANALYZE`, reescritura de LINQ, sugerencias de IA, profiling de CPU/memoria ni un APM de producción.

---

## Documentación

- [Query fingerprints](docs/query-fingerprints.md)
- [Possible N+1](docs/possible-n-plus-one.md)
- [Timing](docs/timing.md)
- [Parameter security](docs/parameter-security.md)
- [Concurrency](docs/concurrency.md)
- [Initial issues](docs/initial-issues.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Changelog](CHANGELOG.md)

---

## Licencia

MIT © Erick Morales
