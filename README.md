# WorkR

[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-blue)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/WorkR?label=WorkR)](https://www.nuget.org/packages/WorkR)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Abstractions?label=WorkR.Abstractions)](https://www.nuget.org/packages/WorkR.Abstractions)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Abstractions?label=WorkR.Triggers.Timers)](https://www.nuget.org/packages/WorkR.Triggers.Timers)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Triggers.AzureStorageQueues?label=WorkR.Triggers.AzureStorageQueues)](https://www.nuget.org/packages/WorkR.Triggers.AzureStorageQueues)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Triggers.AzureServiceBus?label=WorkR.Triggers.AzureServiceBus)](https://www.nuget.org/packages/WorkR.Triggers.AzureServiceBus)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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

WorkR solves this by separating concerns into discrete, composable pieces — **triggers**, **workers**, and **middleware** — each with a single responsibility.

---

## Concepts

| Concept | Role |
|---|---|
| **Trigger** | Owns the execution loop. Fires the worker pipeline on a timer, queue message, or any signal. |
| **Worker** | Receives a value from the trigger (or a previous worker) and performs work. |
| **Middleware** | Wraps worker execution with cross-cutting concerns (error handling, timeouts, scoping). |
| **TriggerContext** | The typed payload passed from trigger to worker chain, carrying metadata like `ExecutionId` and `OccurredAt`. |

---

## Quick Start

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

// Run once on startup
builder.Services.AddRunOnceWorker<MyWorker>();

// Run on a fixed delay
builder.Services.AddDelayWorker<MyWorker>(TimeSpan.FromSeconds(30));

// Run on a cron schedule
builder.Services.AddScheduledWorker<MyWorker>("*/5 * * * *");
```

---

## Tracing

WorkR emits one OpenTelemetry-compatible span per worker pipeline execution from an
`ActivitySource` named `"WorkR"`, regardless of trigger type. Enable it by adding the
source to your OpenTelemetry `TracerProviderBuilder`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("WorkR"));
```

Spans are named `EXECUTE <pipeline>`, where `<pipeline>` is the worker chain joined
with ` -> ` (for example `EXECUTE ValidateOrder -> ShipOrder`), and carry the
following tags:

| Tag | Description |
| --- | --- |
| `workr.version` | Version of the WorkR assembly |
| `workr.service.id` | Stable identifier for the worker service instance |
| `workr.trigger` | Trigger type name |
| `workr.trigger.version` | Version of the trigger's assembly |
| `workr.pipeline` | Worker chain, joined with ` -> ` |
| `workr.execution.id` | Identifier for this individual execution |

When a span is already active (e.g. a messaging SDK's process span), the WorkR span
becomes its child; otherwise it starts a new trace. Failed executions are marked with
an error status and the exception is recorded. When no listener is subscribed, tracing
has no overhead.

The source name `"WorkR"` is a stable public contract.

---

## Packages

| Package | Description |
|---|---|
| [`WorkR.Abstractions`](src/WorkR.Abstractions/README.md) | Core interfaces: `ITrigger<T>`, `IWorker<T>`, `IWorkerMiddleware`, `TriggerContext`. Reference this from libraries that define reusable workers, triggers, or middleware. |
| [`WorkR`](src/WorkR/README.md) | Core implementation: pipeline builder, built-in middleware, `AddWorker`, `AddRunOnceWorker`. |
| [`WorkR.Triggers.Timers`](src/WorkR.Triggers.Timers/README.md) | Delay and cron-scheduled triggers: `AddDelayWorker`, `AddScheduledWorker`. |
| [`WorkR.Triggers.AzureStorageQueues`](src/WorkR.Triggers.AzureStorageQueues/README.md) | Azure Storage Queue trigger: `AddStorageQueueWorker`. |
| [`WorkR.Triggers.AzureServiceBus`](src/WorkR.Triggers.AzureServiceBus/README.md) | Azure Service Bus trigger: `AddServiceBusWorker`. |

---

## License

MIT
