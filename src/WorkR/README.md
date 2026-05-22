# WorkR

[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-blue)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/WorkR)](https://www.nuget.org/packages/WorkR)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/alterius/WorkR/blob/master/LICENSE)

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

Default middleware: `UseScope`. This is applied to the first worker in the chain only — workers in a chain inherit the scope but have no additional middleware unless configured explicitly.

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
        WorkerDelegate<IReadOnlyList<Order>> next,
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
    public async Task ExecuteAsync(WorkerDelegate<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await workerPipeline(new EmptyTriggerContext(DateTimeOffset.UtcNow), stoppingToken);
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

Workers are registered with the DI container automatically. The default lifetime is `Transient`:

```csharp
// Override lifetime
pipeline.AddWorker<MyWorker>(ServiceLifetime.Scoped)

// Skip registration (if already registered elsewhere)
pipeline.AddWorker<MyWorker>(lifetime: null)
```

---

## Middleware

Middleware is configured per worker step using `MiddlewarePipelineBuilder`. It wraps worker execution and is applied in registration order (outermost first).

### `UseScope`

Creates a new `IServiceScope` per execution. Workers downstream of `UseScope` resolve their dependencies from the scoped container.

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
    .UseScope()                            // create DI scope for this execution
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
