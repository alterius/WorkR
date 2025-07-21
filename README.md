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

A trigger is the entry point to a pipeline. It owns the execution loop and is responsible for firing the worker pipeline on a schedule, in response to a queue message, or any other signal. Triggers are long-lived singletons constructed explicitly — they are not resolved from the DI container.

```csharp
public interface ITrigger<out TOut>
{
    Task Execute(Func<TOut, CancellationToken, Task> next, CancellationToken stoppingToken);
}
```

A trigger calls `next` to pass a signal value into the downstream worker chain.

### Workers

Workers receive a value from the trigger (or a previous worker) and perform work. They are resolved from the DI container per execution within their own scope.

A terminating worker receives a value and does work:

```csharp
public interface IWorker<in TIn>
{
    Task Execute(TIn source, CancellationToken ct);
}
```

A piped worker receives a value, does work, and emits a new value to the next worker in the chain:

```csharp
public interface IWorker<in TIn, out TOut>
{
    Task Execute(TIn source, Func<TOut, CancellationToken, Task> next, CancellationToken ct);
}
```

### Middleware

Middleware wraps worker execution with cross-cutting concerns such as error handling, timeouts, and DI scope management. Middleware is configured per worker and composed at registration time — there is zero overhead at execution time.

```csharp
public interface IMiddleware
{
    Task Execute(IServiceProvider sp, MiddlewareDelegate next, CancellationToken ct);
}
```

---

## Getting Started

### Installation

```
dotnet add package WorkR
dotnet add package WorkR.Triggers.Timers
```

### Defining a Worker

```csharp
public class HelloWorldWorker : IWorker<TimerSignal>
{
    private readonly ILogger<HelloWorldWorker> _logger;

    public HelloWorldWorker(ILogger<HelloWorldWorker> logger)
    {
        _logger = logger;
    }

    public Task Execute(TimerSignal signal, CancellationToken ct)
    {
        _logger.LogInformation("Hello world! Triggered at {timestamp}", signal.TriggerTimestamp);
        return Task.CompletedTask;
    }
}
```

### Registering a Pipeline

```csharp
// Fire every 30 seconds
builder.Services.AddDelayWorker<HelloWorldWorker>(TimeSpan.FromSeconds(30));

// Fire on a cron schedule (supports seconds)
builder.Services.AddScheduledWorker<HelloWorldWorker>("*/5 * * * * *");

// Fire on startup and then on a cron schedule
builder.Services.AddScheduledWorker<HelloWorldWorker>("0 0 9 * * *", runOnStartup: true);
```

---

## Triggers

### WorkR.Triggers.Timers

Provides two trigger types for time-based pipelines. Both emit a `TimerSignal` containing the trigger timestamp.

#### TimerSignal

```csharp
public sealed class TimerSignal
{
    public required DateTimeOffset TriggerTimestamp { get; init; }
}
```

#### AddDelayWorker

Fires after a fixed delay between each execution:

```csharp
builder.Services.AddDelayWorker<MyWorker>(TimeSpan.FromSeconds(30));
```

Default middleware: `UseScope` → `UseErrorHandling`.

#### AddScheduledWorker

Fires on a cron schedule with optional second-level precision:

```csharp
builder.Services.AddScheduledWorker<MyWorker>("*/30 * * * * *");
```

Default middleware: `UseFireAndForget` → `UseScope` → `UseErrorHandling`.

The `runOnStartup` parameter causes the pipeline to fire immediately on host start before the schedule takes over:

```csharp
builder.Services.AddScheduledWorker<MyWorker>("0 0 9 * * *", runOnStartup: true);
```

---

## Middleware

Middleware is configured per worker using the `MiddlewarePipelineBuilder`. WorkR ships with the following built-in middleware:

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

Implement `IMiddleware` to create reusable cross-cutting behaviour:

```csharp
public class TracingMiddleware : IMiddleware
{
    public async Task Execute(IServiceProvider sp, MiddlewareDelegate next, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("worker.execute");
        await next(sp, ct).ConfigureAwait(false);
    }
}
```

Register it using `UseMiddleware`:

```csharp
middleware.UseMiddleware<TracingMiddleware>()

// With constructor parameters
middleware.UseMiddleware<TracingMiddleware>(someConfig)

// With a factory
middleware.UseMiddleware(sp => new TracingMiddleware(sp.GetRequiredService<ITracer>()))
```

### Middleware Ordering

Middleware is applied in registration order, outermost first. A typical ordering would be:

```csharp
middleware
    .UseFireAndForget()                   // return to trigger immediately
    .UseScope()                           // create DI scope for this execution
    .UseErrorHandling()                   // catch any exceptions within the scope
    .UseTimeout(TimeSpan.FromSeconds(30)) // cancel if too slow
```

---

## Chained Workers

Workers can be chained together using `IWorker<TIn, TOut>`. Each worker in the chain receives a value, performs work, and calls `next` to emit a value to the next worker.

```csharp
public class MultiplyWorker : IWorker<int, int>
{
    public async Task Execute(int source, Func<int, CancellationToken, Task> next, CancellationToken ct)
    {
        await next(source * 10, ct);
    }
}

public class ConvertToStringWorker : IWorker<int, string>
{
    public async Task Execute(int source, Func<string, CancellationToken, Task> next, CancellationToken ct)
    {
        await next(source.ToString(), ct);
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
builder.Services.AddWorker<RandomNumberTrigger, int>(
    _ => new RandomNumberTrigger(),
    builder => builder
        .RegisterWorker<MultiplyWorker, int>()
        .RegisterWorker<ConvertToStringWorker, string>()
        .RegisterWorker<PrintWorker>());
```

Middleware can be configured per step:

```csharp
builder.Services.AddWorker<RandomNumberTrigger, int>(
    _ => new RandomNumberTrigger(),
    builder => builder
        .RegisterWorker<MultiplyWorker, int>(middleware: mw => mw.UseErrorHandling())
        .RegisterWorker<ConvertToStringWorker, string>()
        .RegisterWorker<PrintWorker>());
```

---

## Worker Lifetime

Workers are registered with the DI container automatically when using `RegisterWorker`. The default lifetime is `Transient`. This can be overridden per worker:

```csharp
builder.RegisterWorker<MyWorker>(ServiceLifetime.Scoped)
```

Pass `null` to skip automatic registration if you have already registered the worker yourself:

```csharp
builder.RegisterWorker<MyWorker>(lifetime: null)
```

---

## Custom Triggers

Implement `ITrigger<TOut>` and define a corresponding signal type:

```csharp
public class MySignal
{
    public required string Payload { get; init; }
}

public class MyTrigger : ITrigger<MySignal>
{
    public async Task Execute(Func<MySignal, CancellationToken, Task> next, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var payload = await PollForWork(stoppingToken);

            await next(new MySignal { Payload = payload }, stoppingToken);
        }
    }
}
```

Register it using `AddWorker`:

```csharp
builder.Services.AddWorker<MyTrigger, MySignal>(
    sp => ActivatorUtilities.CreateInstance<MyTrigger>(sp, myConfig),
    builder => builder.RegisterWorker<MyWorker>());
```

---

## Packages

| Package | Description |
|---|---|
| `WorkR.Abstractions` | Core interfaces (`ITrigger<T>`, `IWorker<T>`, `IWorker<TIn, TOut>`, `IMiddleware`, `MiddlewareDelegate`). Reference this from libraries defining reusable workers, triggers, or middleware. |
| `WorkR` | Core implementation, pipeline builder, built-in middleware, `AddWorker`. |
| `WorkR.Triggers.Timers` | Delay and cron-scheduled triggers (`AddDelayWorker`, `AddScheduledWorker`). |

---

## License

MIT
