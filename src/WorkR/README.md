# WorkR

[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-blue)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/WorkR)](https://www.nuget.org/packages/WorkR)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/alterius/WorkR/blob/master/LICENSE)

> [!IMPORTANT]
> Heads up — WorkR is still in development. Expect breaking API changes before v1.0.

A lightweight .NET background worker framework built on top of `IHostedService`. Define workers and triggers, compose middleware, and let WorkR manage the execution loop.

---

## Installation

```
dotnet add package WorkR
```

---

## Getting Started

### Define a Worker

```csharp
public class MyWorker : IWorker<EmptyTriggerContext>
{
    private readonly ILogger<MyWorker> _logger;

    public MyWorker(ILogger<MyWorker> logger) => _logger = logger;

    public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running at {timestamp}", context.OccurredAt);
        return Task.CompletedTask;
    }
}
```

### Register a Worker

```csharp
// Fire exactly once when the host starts
builder.Services.AddRunOnceWorker<MyWorker>();

// Full control over the pipeline
builder.Services.AddRunOnceWorker(pipeline =>
    pipeline.AddWorker<MyWorker>());
```

---

## `AddRunOnceWorker`

Fires the pipeline exactly once when the host starts. Useful for startup tasks, migrations, or one-off initialisation.

```csharp
builder.Services.AddRunOnceWorker<MyStartupWorker>();
```

Each pipeline execution runs inside its own dependency injection scope, created automatically. Scoped services resolved during the execution share that scope and are disposed when the execution completes.

---

## Chained Workers

Workers can be chained using `IWorker<TIn, TOut>`. Each worker transforms the value and calls `next` to pass it to the next step.

```csharp
public class FetchWorker : IWorker<EmptyTriggerContext, IReadOnlyList<Order>>
{
    private readonly IOrderRepository _repo;

    public FetchWorker(IOrderRepository repo) => _repo = repo;

    public async Task ExecuteAsync(
        EmptyTriggerContext context,
        Worker<IReadOnlyList<Order>> next,
        CancellationToken cancellationToken)
    {
        var orders = await _repo.GetPendingAsync(cancellationToken);
        await next(orders, cancellationToken);
    }
}

public class ProcessWorker : IWorker<IReadOnlyList<Order>>
{
    public async Task ExecuteAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        foreach (var order in orders)
        {
            // process each order
        }
    }
}
```

Register the chain using `AddWorker`:

```csharp
builder.Services.AddRunOnceWorker(pipeline =>
    pipeline
        .AddWorker<FetchWorker, IReadOnlyList<Order>>()
        .AddWorker<ProcessWorker>());
```

---

## Custom Triggers

Implement `ITrigger<TContext>` to define a trigger with any execution model, then register it with `AddWorker`:

```csharp
public class MyTrigger : ITrigger<EmptyTriggerContext>
{
    public async Task ExecuteAsync(IWorkerPipeline<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await workerPipeline.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}

builder.Services.AddWorker<MyTrigger, EmptyTriggerContext>(
    sp => ActivatorUtilities.CreateInstance<MyTrigger>(sp),
    pipeline => pipeline.AddWorker<MyWorker>());
```

---

## Worker Lifetime

There are two ways to add a worker to a pipeline.

### By type (DI-registered)

The worker type is registered with the DI container automatically and resolved per
execution. The default lifetime is `Transient`:

```csharp
// Override lifetime
pipeline.AddWorker<MyWorker>(ServiceLifetime.Scoped)

// Skip registration (if already registered elsewhere)
pipeline.AddWorker<MyWorker>(lifetime: null)
```

### By factory (not DI-registered)

Pass a factory to control construction yourself. The worker is **not** registered with
the DI container, and the factory is invoked **once per execution** — giving you a fresh
instance each run by default:

```csharp
// A new instance is built for every execution
pipeline.AddWorker(sp => new MyWorker(sp.GetRequiredService<IDependency>()))
```

Because the factory is keyed to this position in the pipeline rather than to the worker
type, the same worker type can be added to multiple pipelines (or multiple times in one
pipeline) with independent construction. If you want a single instance shared across
executions, capture it in the closure and return it:

```csharp
var shared = new MyWorker();
pipeline.AddWorker(_ => shared) // same instance every execution — must be thread-safe
```

The factory owns the instance's lifetime — WorkR does not dispose workers it did not
create. If your worker is `IDisposable`/`IAsyncDisposable`, either resolve it from `sp` so
the container disposes it, or manage disposal yourself.

---

## Middleware

Middleware is configured per worker step using `WorkerMiddlewarePipelineBuilder`. It wraps worker execution and is applied in registration order (outermost first).

> **Note:** Every pipeline execution already runs inside its own dependency injection scope, created automatically. You do not need `UseScope` to obtain a scope — scoped services resolved during an execution share that scope and are disposed when the execution completes.

### `UseScope`

Creates a new `IServiceScope` per execution. Workers downstream of `UseScope` resolve their dependencies from the scoped container. This is rarely needed now that each execution already runs in its own scope, but remains available for nesting an additional scope.

```csharp
middleware.UseScope()
```

### `UseErrorHandling`

Catches and swallows exceptions to prevent a failing execution from crashing the pipeline. An optional predicate filters which exceptions are handled.

```csharp
middleware.UseErrorHandling<Exception>()
middleware.UseErrorHandling<HttpRequestException>()
middleware.UseErrorHandling<HttpRequestException>(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable)
```

### `UseTimeout`

Cancels execution if it exceeds the specified duration.

```csharp
middleware.UseTimeout(TimeSpan.FromSeconds(30))
```

### Custom Middleware

Implement `IWorkerMiddleware` to create reusable cross-cutting behaviour:

```csharp
public class TracingMiddleware : IWorkerMiddleware
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
    {
        using var activity = Activity.StartActivity("worker.execute");
        await next(cancellationToken);
    }
}
```

Register it with `UseMiddleware`:

```csharp
// Resolved via factory (access to DI)
middleware.UseMiddleware(sp => new TracingMiddleware(sp.GetRequiredService<ITracer>()))

// Pre-constructed instance
middleware.UseMiddleware(new TracingMiddleware())
```

### Middleware Ordering

A typical ordering:

```csharp
middleware
    .UseErrorHandling<Exception>()         // catch any exceptions from this or subsequent workers
    .UseTimeout(TimeSpan.FromSeconds(30))  // cancel if too slow
```

Middleware can be configured per step in a chain and effects all subsequent steps:

```csharp
builder.Services.AddRunOnceWorker(pipeline =>
    pipeline
        .AddWorker<FetchWorker, IReadOnlyList<Order>>(middleware: mw => mw.UseTimeout(TimeSpan.FromSeconds(10)))
        .AddWorker<ProcessWorker>());
```
