# WorkR.Triggers.AzureServiceBus

[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-blue)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/WorkR.Triggers.AzureServiceBus)](https://www.nuget.org/packages/WorkR.Triggers.AzureServiceBus)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/alterius/WorkR/blob/master/LICENSE)

Azure Service Bus trigger for [WorkR](https://github.com/alterius/WorkR). Processes messages from a queue or topic subscription and drives them through a composable worker pipeline.

---

## Installation

```
dotnet add package WorkR.Triggers.AzureServiceBus
```

---

## `AddServiceBusWorker`

### Typed messages — queue (recommended)

Deserialise Service Bus messages to a strongly-typed model before passing them to your worker. JSON deserialisation is used by default.

```csharp
public class OrderCreatedWorker : IWorker<ServiceBusTriggerContext<OrderCreated>>
{
    public async Task ExecuteAsync(
        ServiceBusTriggerContext<OrderCreated> context,
        CancellationToken cancellationToken)
    {
        var order = context.Value;  // deserialized OrderCreated

        // process the order...

        await context.Args.CompleteMessageAsync(context.Args.Message, cancellationToken);
    }
}

builder.Services.AddServiceBusWorker<OrderCreated, OrderCreatedWorker>(
    sp => sp.GetRequiredService<ServiceBusClient>(),
    "orders");
```

### Typed messages — topic subscription

```csharp
builder.Services.AddServiceBusWorker<OrderCreated, OrderCreatedWorker>(
    sp => sp.GetRequiredService<ServiceBusClient>(),
    topicName: "orders",
    subscriptionName: "fulfillment");
```

### Raw messages

Receive the raw `ProcessMessageEventArgs` without deserialisation:

```csharp
public class RawWorker : IWorker<ServiceBusTriggerContext>
{
    public async Task ExecuteAsync(
        ServiceBusTriggerContext context,
        CancellationToken cancellationToken)
    {
        var args = context.Value;  // ProcessMessageEventArgs
        var body = args.Message.Body.ToString();

        await args.CompleteMessageAsync(args.Message, cancellationToken);
    }
}

builder.Services.AddServiceBusWorker<RawWorker>(
    sp => sp.GetRequiredService<ServiceBusClient>(),
    "my-queue");
```

---

## Worker Contract

Workers must either return a completed task or throw — the trigger does not impose any settlement outcome. Message settlement (complete, abandon, dead-letter, defer) is the worker's responsibility via `context.Args`. If no settlement is performed the SDK's default behaviour applies (controlled by `ServiceBusProcessorOptions.AutoCompleteMessages`, which defaults to `true`).

## `ServiceBusTriggerContext`

The context passed to your worker for each message.

| Member | Description |
|---|---|
| `Value` | The deserialised message body (`T` for typed, `ProcessMessageEventArgs` for raw) |
| `Args` | The raw `ProcessMessageEventArgs` (typed variant only) |
| `ExecutionId` | Unique identifier for this pipeline invocation |
| `OccurredAt` | When the message was received |

Message settlement (complete, abandon, dead-letter, defer) is performed directly via `Args`:

```csharp
await context.Args.CompleteMessageAsync(context.Args.Message, cancellationToken);
await context.Args.AbandonMessageAsync(context.Args.Message, cancellationToken: cancellationToken);
await context.Args.DeadLetterMessageAsync(context.Args.Message, cancellationToken: cancellationToken);
```

---

## Custom Deserialiser

Supply a custom deserialiser to control how message bodies are converted to your model:

```csharp
builder.Services.AddServiceBusWorker<OrderCreated, OrderCreatedWorker>(
    sp => sp.GetRequiredService<ServiceBusClient>(),
    "orders",
    deserializerFactory: sp => async args =>
    {
        var json = args.Message.Body.ToString();
        return JsonSerializer.Deserialize<OrderCreated>(json, myOptions)!;
    });
```

The built-in JSON deserialiser can also be configured with custom `JsonSerializerOptions`:

```csharp
deserializerFactory: _ => ServiceBusMessageDeserializers.Json<OrderCreated>(myJsonOptions)
```

---

## Processor Options

WorkR creates a `ServiceBusProcessor` using the Azure SDK's default `ServiceBusProcessorOptions`. Pass a `configure` delegate to override any of these:

```csharp
builder.Services.AddServiceBusWorker<OrderCreated, OrderCreatedWorker>(
    sp => sp.GetRequiredService<ServiceBusClient>(),
    "orders",
    configure: options =>
    {
        options.MaxConcurrentCalls = 4;
        options.AutoCompleteMessages = false;
    });
```

See [ServiceBusProcessorOptions](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusprocessoroptions) for the full list of available options and their defaults.

---

## Full Pipeline Control

Use the builder overload to chain multiple workers or configure per-step middleware:

```csharp
builder.Services.AddServiceBusWorker<OrderCreated>(
    sp => sp.GetRequiredService<ServiceBusClient>(),
    "orders",
    pipeline => pipeline
        .AddWorker<ValidateWorker, ValidatedOrder>()
        .AddWorker<PersistWorker>());
```

Topic/subscription variant:

```csharp
builder.Services.AddServiceBusWorker<OrderCreated>(
    sp => sp.GetRequiredService<ServiceBusClient>(),
    topicName: "orders",
    subscriptionName: "fulfillment",
    pipeline => pipeline
        .AddWorker<ValidateWorker, ValidatedOrder>()
        .AddWorker<PersistWorker>());
```

Default middleware: `UseScope`. Applied to the first worker in the chain only.
