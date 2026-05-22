# WorkR.Abstractions

[![.NET](https://img.shields.io/badge/.NET-Standard%202.0-blue)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Abstractions)](https://www.nuget.org/packages/WorkR.Abstractions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/alterius/WorkR/blob/master/LICENSE)

Core interfaces and abstractions for the [WorkR](https://github.com/alterius/WorkR) background worker framework.

Reference this package from libraries that define reusable workers, triggers, or middleware. It targets `netstandard2.0` and has no dependencies.

---

## Installation

```
dotnet add package WorkR.Abstractions
```

---

## Interfaces

### `ITrigger<TContext>`

A trigger owns the execution loop and is responsible for calling the downstream worker pipeline. It runs for the lifetime of the host.

```csharp
public interface ITrigger<out TContext>
    where TContext : TriggerContext
{
    Task ExecuteAsync(WorkerDelegate<TContext> workerPipeline, CancellationToken stoppingToken);
}
```

Call `workerPipeline` to pass a context into the downstream worker chain. Triggers are long-lived singletons; the loop logic (polling, waiting, scheduling) lives entirely within `ExecuteAsync`.

### `IWorker<TIn>`

The terminal worker in a pipeline. Receives a context value and performs work.

```csharp
public interface IWorker<in TIn>
{
    Task ExecuteAsync(TIn source, CancellationToken cancellationToken);
}
```

### `IWorker<TIn, TOut>`

A transforming worker. Receives a value, performs optional work, and calls `next` to pass a new value to the next step in the chain.

```csharp
public interface IWorker<in TIn, out TOut>
{
    Task ExecuteAsync(TIn source, WorkerDelegate<TOut> next, CancellationToken cancellationToken);
}
```

### `IWorkerMiddleware`

Cross-cutting behaviour that wraps worker execution. Applied per worker step at registration time.

```csharp
public interface IWorkerMiddleware
{
    Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken);
}
```

---

## Context Types

Every trigger produces a `TriggerContext`. The base class carries metadata about each pipeline invocation:

```csharp
public abstract class TriggerContext
{
    public Guid ExecutionId { get; }        // unique per invocation
    public DateTimeOffset OccurredAt { get; }
}
```

WorkR provides three built-in context types:

| Type | Use case |
|---|---|
| `EmptyTriggerContext` | Time-based triggers with no payload (delay, scheduled, run-once) |
| `ValueTriggerContext<T>` | Triggers that carry a single typed value |
| Custom subclass | Triggers with multiple fields (e.g. message ID + payload + raw message) |

### `ValueTriggerContext<T>`

```csharp
public class ValueTriggerContext<T> : TriggerContext
{
    public T Value { get; }
}
```

---

## Implementing a Custom Trigger

```csharp
public class MyQueueContext : ValueTriggerContext<string>
{
    public MyQueueContext(DateTimeOffset occurredAt, string body, string messageId)
        : base(occurredAt, body)
    {
        MessageId = messageId;
    }

    public string MessageId { get; }
}

public class MyQueueTrigger : ITrigger<MyQueueContext>
{
    private readonly IMyQueue _queue;

    public MyQueueTrigger(IMyQueue queue) => _queue = queue;

    public async Task ExecuteAsync(WorkerDelegate<MyQueueContext> workerPipeline, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await _queue.ReceiveAsync(stoppingToken);

            await workerPipeline(new MyQueueContext(
                DateTimeOffset.UtcNow,
                message.Body,
                message.MessageId), stoppingToken);
        }
    }
}
```
