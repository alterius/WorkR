# WorkR

WorkR is a lightweight, extensible .NET library for building composable background worker pipelines on top of `BackgroundService`. It replaces deeply nested loops and ad-hoc polling logic with a clean, testable, and DI-friendly abstraction.

---

## The Problem

Building background workers in .NET typically results in boilerplate-heavy `BackgroundService` implementations with nested loops, scattered error handling, and logic that is difficult to test in isolation:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            var results = await _repository.QueryAsync();
            foreach (var result in results)
            {
                await _processor.ProcessAsync(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker failed.");
        }

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
}
```

WorkR solves this by separating concerns into discrete, composable pieces — triggers, workers, and middleware — each with a single responsibility.

---

## Concepts

### Triggers

A trigger is the entry point to a pipeline. It owns the execution loop and is responsible for firing the worker pipeline on a schedule, in response to a queue message, or any other signal. Triggers are long-lived singletons that implement `ITrigger<TContext>`:

```csharp
public interface ITrigger<out TContext>
    where TContext : TriggerContext
{
    Task Execute(WorkerDelegate<TContext> next, CancellationToken stoppingToken);
}
```

A trigger calls `next` to pass a `TriggerContext` into the downstream worker chain.

### TriggerContext

Every trigger produces a `TriggerContext`. This is the common base type for all trigger outputs and carries metadata about the execution:

```csharp
public abstract class TriggerContext
{
    public Guid ExecutionId { get; }       // unique per pipeline invocation
    public DateTimeOffset OccurredAt { get; }
}
```

WorkR provides three built-in context types:

| Type | Use case |
|---|---|
| `EmptyTriggerContext` | Time-based triggers with no payload |
| `ValueTriggerContext<T>` | Triggers that carry a single typed value (e.g. a queue message body) |
| Custom subclass | Triggers with multiple fields (e.g. message ID + payload + partition key) |

### Workers

Workers receive a value from the trigger (or a previous worker) and perform work. They are resolved from the DI container per execution within their own scope.

The final worker in a pipeline receives a value and completes execution:

```csharp
public interface IWorker<in TIn>
{
    Task Execute(TIn source, CancellationToken ct);
}
```

A worker earlier in the chain receives a value, transforms it, and passes the result to the next step:

```csharp
public interface IWorker<in TIn, out TOut>
{
    Task Execute(TIn source, WorkerDelegate<TOut> next, CancellationToken ct);
}
```

### Middleware

Middleware wraps worker execution with cross-cutting concerns such as error handling and timeouts. Middleware is configured per worker and composed at registration time.

```csharp
public interface IWorkerMiddleware
{
    Task Execute(Func<CancellationToken, Task> next, CancellationToken ct);
}
```

---

## Getting Started

### Installation

```
dotnet add package WorkR
dotnet add package WorkR.Triggers.Timers  # optional, for timer-based triggers
```

### Defining a Worker

```csharp
public class HelloWorldWorker : IWorker<EmptyTriggerContext>
{
    private readonly ILogger<HelloWorldWorker> _logger;

    public HelloWorldWorker(ILogger<HelloWorldWorker> logger)
    {
        _logger = logger;
    }

    public Task Execute(EmptyTriggerContext context, CancellationToken ct)
    {
        _logger.LogInformation("Hello world! Triggered at {timestamp}", context.OccurredAt);
        return Task.CompletedTask;
    }
}
```

### Registering a Worker

```csharp
// Fire once on startup (using WorkR.Triggers.RunOnce)
builder.Services.AddRunOnceWorker<HelloWorldWorker>();

// Fire after a fixed delay between each execution
builder.Services.AddDelayWorker<HelloWorldWorker>(TimeSpan.FromSeconds(30));

// Fire on a cron schedule
builder.Services.AddScheduledWorker<HelloWorldWorker>("*/5 * * * *");

// Fire on startup and then on a cron schedule
builder.Services.AddScheduledWorker<HelloWorldWorker>("0 9 * * *", runOnStartup: true);
```

---

## Triggers

### AddRunOnceWorker (WorkR.Triggers.RunOnce)

Fires the pipeline exactly once when the host starts. Useful for startup tasks, migrations, or one-off initialisation.

```csharp
using WorkR.Triggers.RunOnce;

builder.Services.AddRunOnceWorker<MyStartupWorker>();
```

Default middleware: `UseScope` → `UseErrorHandling`.

### AddDelayWorker (WorkR.Triggers.Timers)

Fires after a fixed delay between each execution:

```csharp
builder.Services.AddDelayWorker<MyWorker>(TimeSpan.FromSeconds(30));
```

Default middleware: `UseScope` → `UseErrorHandling`.

### AddScheduledWorker (WorkR.Triggers.Timers)

Fires on a cron schedule. Supports second-level precision via `parseOptions`:

```csharp
builder.Services.AddScheduledWorker<MyWorker>("*/30 * * * *");
```

The `runOnStartup` parameter causes the pipeline to fire immediately on host start before the schedule takes over:

```csharp
builder.Services.AddScheduledWorker<MyWorker>("0 9 * * *", runOnStartup: true);
```

Default middleware: `UseFireAndForget` → `UseScope` → `UseErrorHandling`.

---

## Chained Workers

Workers can be chained together using `IWorker<TIn, TOut>`. Each worker in the chain receives a value, transforms it, and calls `next` to pass the result to the next step.

```csharp
public class MultiplyWorker : IWorker<int, int>
{
    public Task Execute(int source, WorkerDelegate<int> next, CancellationToken ct)
    {
        return next(source * 10, ct);
    }
}

public class ConvertToStringWorker : IWorker<int, string>
{
    public Task Execute(int source, WorkerDelegate<string> next, CancellationToken ct)
    {
        return next(source.ToString(), ct);
    }
}

public class PrintWorker : IWorker<string>
{
    private readonly ILogger<PrintWorker> _logger;

    public PrintWorker(ILogger<PrintWorker> logger) => _logger = logger;

    public Task Execute(string source, CancellationToken ct)
    {
        _logger.LogInformation("{value}", source);
        return Task.CompletedTask;
    }
}
```

Register the chain using `AddWorker`:

```csharp
builder.Services.AddDelayWorker(
    TimeSpan.FromSeconds(5),
    builder => builder
        .AddWorker<MultiplyWorker, int>()
        .AddWorker<ConvertToStringWorker, string>()
        .AddWorker<PrintWorker>());
```

Middleware can be configured per step:

```csharp
builder.Services.AddDelayWorker(
    TimeSpan.FromSeconds(5),
    builder => builder
        .AddWorker<MultiplyWorker, int>(middleware: mw => mw.UseErrorHandling())
        .AddWorker<ConvertToStringWorker, string>()
        .AddWorker<PrintWorker>());
```

---

## Custom Triggers

Implement `ITrigger<TContext>` to define a trigger with any execution model. Choose the appropriate context type:

- Use `EmptyTriggerContext` if the trigger has no meaningful payload (e.g. a heartbeat)
- Use `ValueTriggerContext<T>` if the trigger carries a single value (e.g. a queue message body)
- Subclass `TriggerContext` directly for richer payloads

```csharp
public class QueueMessageContext : ValueTriggerContext<string>
{
    public QueueMessageContext(DateTimeOffset occurredAt, string body, string messageId)
        : base(occurredAt, body)
    {
        MessageId = messageId;
    }

    public string MessageId { get; }
}

public class QueueTrigger : ITrigger<QueueMessageContext>
{
    public async Task Execute(WorkerDelegate<QueueMessageContext> next, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await _queue.ReceiveAsync(stoppingToken);

            await next(new QueueMessageContext(
                DateTimeOffset.UtcNow,
                message.Body,
                message.MessageId), stoppingToken);
        }
    }
}
```

Register it using `AddWorker`:

```csharp
builder.Services.AddWorker<QueueTrigger, QueueMessageContext>(
    sp => ActivatorUtilities.CreateInstance<QueueTrigger>(sp),
    builder => builder.AddWorker<MyWorker>());
```

---

## Worker Lifetime

Workers are registered with the DI container automatically when using `AddWorker`. The default lifetime is `Transient`. This can be overridden per worker:

```csharp
builder.AddWorker<MyWorker>(ServiceLifetime.Scoped)
```

Pass `null` to skip automatic registration if you have already registered the worker yourself:

```csharp
builder.AddWorker<MyWorker>(lifetime: null)
```

---

## Middleware

Middleware is configured per worker using `MiddlewarePipelineBuilder`. WorkR ships with the following built-in middleware:

### UseScope

Creates a new `IServiceScope` for each execution and flows the scoped `IServiceProvider` through the rest of the middleware and worker chain. Workers downstream of `UseScope` resolve their dependencies from the scoped container.

```csharp
middleware.UseScope()
```

### UseErrorHandling

Catches exceptions and swallows them to prevent a failing execution from crashing the pipeline. An optional predicate can be used to filter which exceptions are handled:

```csharp
middleware.UseErrorHandling()
middleware.UseErrorHandling<HttpRequestException>()
middleware.UseErrorHandling<HttpRequestException>(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable)
```

### UseTimeout

Cancels execution if it exceeds the specified duration:

```csharp
middleware.UseTimeout(TimeSpan.FromSeconds(30))
```

### UseFireAndForget

Dispatches execution to the thread pool and returns immediately, allowing the trigger to continue without waiting for the worker chain to complete:

```csharp
middleware.UseFireAndForget()
```

### Custom Middleware

Implement `IWorkerMiddleware` to create reusable cross-cutting behaviour:

```csharp
public class TracingMiddleware : IWorkerMiddleware
{
    private readonly ITracer _tracer;

    public TracingMiddleware(ITracer tracer)
    {
        _tracer = tracer;
    }

    public async Task Execute(Func<CancellationToken, Task> next, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("worker.execute");
        await next(ct).ConfigureAwait(false);
    }
}
```

Register it using `UseMiddleware`:

```csharp
// Resolved via factory (access to DI)
middleware.UseMiddleware(sp => new TracingMiddleware(sp.GetRequiredService<ITracer>()))

// Pre-constructed instance
middleware.UseMiddleware(new TracingMiddleware(tracer))
```

### Middleware Ordering

Middleware is applied in registration order, outermost first. A typical ordering would be:

```csharp
middleware
    .UseFireAndForget()                    // return to trigger immediately
    .UseScope()                            // create DI scope for this execution
    .UseErrorHandling()                    // catch any exceptions within the scope
    .UseTimeout(TimeSpan.FromSeconds(30))  // cancel if too slow
```

---

## Packages

| Package | Description |
|---|---|
| `WorkR.Abstractions` | Core abstractions: `ITrigger<T>`, `IWorker<T>`, `IWorker<TIn, TOut>`, `IWorkerMiddleware`, `TriggerContext`, `EmptyTriggerContext`, `ValueTriggerContext<T>`, `WorkerDelegate<T>`. Reference this from libraries defining reusable workers, triggers, or middleware. |
| `WorkR` | Core implementation: pipeline builder, built-in middleware, `AddWorker`. Includes `WorkR.Triggers.RunOnce` (`RunOnceTrigger`, `AddRunOnceWorker`) — no separate package needed. |
| `WorkR.Triggers.Timers` | Delay and cron-scheduled triggers: `AddDelayWorker`, `AddScheduledWorker`. |

---

## License

MIT
