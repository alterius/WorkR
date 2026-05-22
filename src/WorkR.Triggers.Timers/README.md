# WorkR.Triggers.Timers

[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-blue)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Triggers.Timers)](https://www.nuget.org/packages/WorkR.Triggers.Timers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/alterius/WorkR/blob/master/LICENSE)

Timer-based triggers for [WorkR](https://github.com/alterius/WorkR), supporting fixed-delay and cron-scheduled background workers.

---

## Installation

```
dotnet add package WorkR.Triggers.Timers
```

---

## `AddDelayWorker`

Fires the pipeline repeatedly, waiting a fixed delay between the end of one execution and the start of the next.

### Simple registration

```csharp
builder.Services.AddDelayWorker<MyWorker>(TimeSpan.FromSeconds(30));
```

### With startup execution

Run the pipeline immediately on host start, then continue on the delay interval:

```csharp
builder.Services.AddDelayWorker<MyWorker>(TimeSpan.FromSeconds(30), runOnStartup: true);
```

### With full pipeline control

```csharp
builder.Services.AddDelayWorker(
    TimeSpan.FromSeconds(30),
    pipeline => pipeline
        .AddWorker<FetchWorker, IReadOnlyList<Item>>()
        .AddWorker<ProcessWorker>());
```

Default middleware: `UseScope`. Applied to the first worker in the chain only.

---

## `AddScheduledWorker`

Fires the pipeline on a [cron schedule](https://github.com/atifaziz/NCrontab). Standard five-field cron expressions are supported by default.

### Simple registration

```csharp
// Every 5 minutes
builder.Services.AddScheduledWorker<MyWorker>("*/5 * * * *");

// Every day at 9am
builder.Services.AddScheduledWorker<MyWorker>("0 9 * * *");
```

### With startup execution

Run immediately on host start before the schedule takes over:

```csharp
builder.Services.AddScheduledWorker<MyWorker>("0 9 * * *", runOnStartup: true);
```

### With second-level precision

Enable six-field cron expressions (seconds field prepended):

```csharp
// Every 30 seconds
builder.Services.AddScheduledWorker<MyWorker>("*/30 * * * * *", includeSeconds: true);
```

### With overlap cancellation

Cancel a still-running execution when the next scheduled firing arrives:

```csharp
builder.Services.AddScheduledWorker<MyWorker>("*/5 * * * *", cancelOnOverlap: true);
```

When `cancelOnOverlap` is `true` the previous execution's `CancellationToken` is cancelled before the new one starts. The old execution is not awaited — it drains in the background while the new one proceeds.

### With full pipeline control

```csharp
builder.Services.AddScheduledWorker(
    "0 * * * *",
    pipeline => pipeline
        .AddWorker<FetchWorker, IReadOnlyList<Item>>()
        .AddWorker<ProcessWorker>());
```

Default middleware: `UseScope`. Applied to the first worker in the chain only.

> `ScheduledTrigger` fires worker executions on the thread pool so that a long-running execution does not delay the next scheduled firing.

---

## Worker Interface

Both triggers use `EmptyTriggerContext` — time-based triggers carry no message payload.

```csharp
public class MyWorker : IWorker<EmptyTriggerContext>
{
    private readonly ILogger<MyWorker> _logger;

    public MyWorker(ILogger<MyWorker> logger) => _logger = logger;

    public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Triggered at {timestamp} (execution {id})",
            context.OccurredAt, context.ExecutionId);
        return Task.CompletedTask;
    }
}
```
