# WorkR.Triggers.AzureStorageQueues

[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-blue)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Triggers.AzureStorageQueues)](https://www.nuget.org/packages/WorkR.Triggers.AzureStorageQueues)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/alterius/WorkR/blob/master/LICENSE)

> [!IMPORTANT]
> Heads up — WorkR is still in development. Expect breaking API changes before v1.0.

Azure Storage Queue polling trigger for [WorkR](https://github.com/alterius/WorkR). Polls a queue, [optionally] deserialises messages, and passes them through a composable worker pipeline.

---

## Installation

```
dotnet add package WorkR.Triggers.AzureStorageQueues
```

---

## `AddStorageQueueWorker`

### Typed messages (recommended)

Deserialise queue messages to a strongly-typed model before passing them to your worker. JSON deserialisation is used by default.

```csharp
public class OrderCreatedWorker : IWorker<StorageQueueTriggerContext<OrderCreated>>
{
    public async Task ExecuteAsync(
        StorageQueueTriggerContext<OrderCreated> context,
        CancellationToken cancellationToken)
    {
        var order = context.Value;  // deserialized OrderCreated

        await ProcessAsync(order, cancellationToken);

        await context.DeleteMessageAsync(cancellationToken);
    }
}

builder.Services.AddStorageQueueWorker<OrderCreated, OrderCreatedWorker>(
    sp => sp.GetRequiredService<QueueServiceClient>(),
    "orders");
```

### Raw messages

Receive the raw `QueueMessage` without deserialisation:

```csharp
public class RawWorker : IWorker<StorageQueueTriggerContext>
{
    public async Task ExecuteAsync(
        StorageQueueTriggerContext context,
        CancellationToken cancellationToken)
    {
        var body = context.Value.Body.ToString();  // raw message body

        await ProcessAsync(body, cancellationToken);

        await context.DeleteMessageAsync(cancellationToken);
    }
}

builder.Services.AddStorageQueueWorker<RawWorker>(
    sp => sp.GetRequiredService<QueueServiceClient>(),
    "my-queue");
```

---

## `StorageQueueTriggerContext`

The context passed to your worker for each message.

| Member | Description |
|---|---|
| `Value` | The deserialized message body (`T` for typed, `QueueMessage` for raw) |
| `Message` | The raw `QueueMessage` (typed variant only) |
| `ExecutionId` | Unique identifier for this pipeline invocation |
| `OccurredAt` | When the message was received |
| `DeleteMessageAsync(ct)` | Deletes the message from the queue |
| `DeadLetterMessageAsync(ct)` | Moves the message to the poison queue (`<queue-name>-poison`) and deletes it from the main queue |

---

## Worker Contract

Each message is processed on the thread pool independently of the polling loop. Workers must either return a completed task or throw — the trigger handles settlement based on the outcome and the configured options.

## Message Completion

By default (`AutoCompleteMessages = true`) the trigger deletes the message automatically after the worker returns successfully. If the worker throws, the message is left on the queue and will become visible again after `VisibilityTimeout`. Once the message has been dequeued `MaxDeliveryCount` times it is automatically moved to the poison queue.

To take manual control, set `AutoCompleteMessages = false` and call `DeleteMessageAsync` or `DeadLetterMessageAsync` yourself:

```csharp
builder.Services.AddStorageQueueWorker<OrderCreated, OrderCreatedWorker>(
    sp => sp.GetRequiredService<QueueServiceClient>(),
    "orders",
    configure: options => options.AutoCompleteMessages = false);
```

```csharp
public async Task ExecuteAsync(StorageQueueTriggerContext<OrderCreated> context, CancellationToken cancellationToken)
{
    try
    {
        await ProcessAsync(context.Value, cancellationToken);
        await context.DeleteMessageAsync(cancellationToken);
    }
    catch (NonRetryableException)
    {
        await context.DeadLetterMessageAsync(cancellationToken);
    }
}
```

---

## Custom Deserialiser

Supply a custom deserialiser to control how message bodies are converted to your model:

```csharp
builder.Services.AddStorageQueueWorker<OrderCreated, OrderCreatedWorker>(
    sp => sp.GetRequiredService<QueueServiceClient>(),
    "orders",
    deserializerFactory: sp => async message =>
    {
        var json = message.Body.ToString();
        return JsonSerializer.Deserialize<OrderCreated>(json, myOptions)!;
    });
```

The built-in JSON deserialiser can also be configured with custom `JsonSerializerOptions`:

```csharp
deserializerFactory: _ => StorageQueueMessageDeserializers.Json<OrderCreated>(myJsonOptions)
```

---

## `StorageQueueTriggerOptions`

| Option | Default | Description |
|---|---|---|
| `AutoCompleteMessages` | `true` | Delete the message automatically after successful processing |
| `MaxDeliveryCount` | `5` | Times a message may be dequeued and fail before being automatically dead-lettered (0 to disable) |
| `MaxConcurrentCalls` | `1` | Maximum number of worker executions running concurrently. The polling loop applies backpressure once this limit is reached. |
| `MaxMessages` | `1` | Maximum messages retrieved per poll (1–32) |
| `VisibilityTimeout` | `30s` | How long a received message remains invisible to other consumers while being processed |
| `PollingDelay` | Fixed 5s | Wait strategy when the queue is empty |
| `ErrorDelay` | Fixed 5s | Wait strategy after a transient receive error |

`MaxConcurrentCalls` and `MaxMessages` are independent. Set `MaxMessages` to control how many messages are fetched in one network call; set `MaxConcurrentCalls` to control how many workers run in parallel.

`PollingDelay` and `ErrorDelay` accept a `StorageQueueDelay` delegate — use `StorageQueueDelayStrategy` for built-in strategies:

```csharp
configure: options =>
{
    options.PollingDelay = StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(10));
    options.MaxMessages = 8;
}
```

---

## Full Pipeline Control

Use the builder overload to chain multiple workers or configure per-step middleware:

```csharp
builder.Services.AddStorageQueueWorker<OrderCreated>(
    sp => sp.GetRequiredService<QueueServiceClient>(),
    "orders",
    pipeline => pipeline
        .AddWorker<ValidateWorker, ValidatedOrder>()
        .AddWorker<PersistWorker>());
```

Each pipeline execution runs inside its own dependency injection scope, created automatically.
