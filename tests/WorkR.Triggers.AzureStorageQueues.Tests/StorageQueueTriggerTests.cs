using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

[Trait("Category", "L0")]
public class StorageQueueTriggerTests
{
    private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly StorageQueueTriggerOptions DefaultOptions = new();
    private const string QueueName = "test-queue";

    private static (QueueServiceClient ServiceClient, QueueClient QueueClient) SubClients()
    {
        var queueClient = Substitute.For<QueueClient>();
        var serviceClient = Substitute.For<QueueServiceClient>();
        serviceClient.GetQueueClient(QueueName).Returns(queueClient);
        return (serviceClient, queueClient);
    }

    private static QueueMessage MakeMessage(string body = "{}", string messageId = "msg-1", string popReceipt = "pop-1") =>
        QueuesModelFactory.QueueMessage(messageId, popReceipt, BinaryData.FromString(body), 0);

    private static Response<QueueMessage[]> MessagesResponse(params QueueMessage[] messages)
    {
        var response = Substitute.For<Response<QueueMessage[]>>();
        response.Value.Returns(messages);
        return response;
    }

    private static Response<QueueMessage[]> EmptyResponse() => MessagesResponse();

    private static StorageQueueTrigger MakeTrigger(QueueServiceClient serviceClient, StorageQueueTriggerOptions? options = null) =>
        new(serviceClient, QueueName, options ?? DefaultOptions, new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger>());

    // ---------------------------------------------------------------------------
    // Constructor guards
    // ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_WhenQueueServiceClientIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger(null!, QueueName, DefaultOptions, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenQueueNameIsNull_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), null!, DefaultOptions, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenQueueNameIsWhiteSpace_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), "   ", DefaultOptions, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenConfigIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), QueueName, null!, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), QueueName, DefaultOptions, null!, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), QueueName, DefaultOptions, TimeProvider.System, null!));

    // ---------------------------------------------------------------------------
    // Polling behaviour
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WhenQueueIsEmpty_DoesNotInvokeWorker()
    {
        var (serviceClient, queueClient) = SubClients();
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(emptyResponse);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient);
        var invoked = false;

        var executeTask = trigger.ExecuteAsync((ctx, ct) => { invoked = true; return Task.CompletedTask; }, cts.Token);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        invoked.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenQueueHasMessages_InvokesWorkerOncePerMessage()
    {
        var messages = new[] { MakeMessage(messageId: "a"), MakeMessage(messageId: "b") };
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(messages);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var invocations = new List<string>();
        var bothInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient);

        var executeTask = trigger.ExecuteAsync((ctx, ct) =>
        {
            invocations.Add(ctx.Value.MessageId);
            if (invocations.Count == 2) bothInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await bothInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        invocations.ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task WhenQueueIsEmpty_WaitsPollingDelayBeforeRetrying()
    {
        var options = new StorageQueueTriggerOptions { PollingDelay = StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(10)) };
        var secondPollStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var pollCount = 0;
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                pollCount++;
                if (pollCount == 2) secondPollStarted.TrySetResult();
                return EmptyResponse();
            });
        var timeProvider = new FakeTimeProvider(StartTime);
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, options, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        pollCount.ShouldBe(1);
        timeProvider.Advance(TimeSpan.FromSeconds(9));
        pollCount.ShouldBe(1);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await secondPollStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task WhenRequestFailedWithRetryableStatus_ContinuesPollingAfterErrorDelay(int status)
    {
        var recoveredPollStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var threw = false;
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (!threw) { threw = true; throw new RequestFailedException(status, "Transient"); }
                recoveredPollStarted.TrySetResult();
                return EmptyResponse();
            });
        var timeProvider = new FakeTimeProvider(StartTime);
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultOptions, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        timeProvider.Advance(DefaultOptions.ErrorDelay(0));
        await recoveredPollStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    [Fact]
    public async Task WhenHttpRequestExceptionThrown_ContinuesPollingAfterErrorDelay()
    {
        var recoveredPollStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var threw = false;
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (!threw) { threw = true; throw new HttpRequestException("Network error"); }
                recoveredPollStarted.TrySetResult();
                return EmptyResponse();
            });
        var timeProvider = new FakeTimeProvider(StartTime);
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultOptions, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        timeProvider.Advance(DefaultOptions.ErrorDelay(0));
        await recoveredPollStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    [Fact]
    public async Task ErrorDelay_ReceivesIncrementingErrorCount()
    {
        var secondDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var capturedCounts = new List<int>();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns<Response<QueueMessage[]>>(_ => throw new RequestFailedException(429, "Transient"));
        var timeProvider = new FakeTimeProvider(StartTime);
        var options = new StorageQueueTriggerOptions
        {
            ErrorDelay = count =>
            {
                capturedCounts.Add(count);
                if (capturedCounts.Count == 2) secondDelayStarted.TrySetResult();
                return TimeSpan.FromSeconds(1);
            }
        };
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, options, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await secondDelayStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        capturedCounts.Take(2).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task ErrorDelay_ResetsToZeroAfterSuccessfulReceive()
    {
        var secondDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var capturedCounts = new List<int>();
        var callCount = 0;
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1 || callCount >= 3) throw new RequestFailedException(429, "Transient");
                return EmptyResponse();
            });
        var timeProvider = new FakeTimeProvider(StartTime);
        var options = new StorageQueueTriggerOptions
        {
            ErrorDelay = count =>
            {
                capturedCounts.Add(count);
                if (capturedCounts.Count == 2) secondDelayStarted.TrySetResult();
                return TimeSpan.FromSeconds(1);
            },
            PollingDelay = _ => TimeSpan.FromSeconds(1)
        };
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, options, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await secondDelayStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        capturedCounts.Take(2).ShouldBe([0, 0]);
    }

    [Fact]
    public async Task PollingDelay_ReceivesIncrementingEmptyPollCount()
    {
        var secondDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var capturedCounts = new List<int>();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ => EmptyResponse());
        var timeProvider = new FakeTimeProvider(StartTime);
        var options = new StorageQueueTriggerOptions
        {
            PollingDelay = count =>
            {
                capturedCounts.Add(count);
                if (capturedCounts.Count == 2) secondDelayStarted.TrySetResult();
                return TimeSpan.FromSeconds(1);
            }
        };
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, options, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await secondDelayStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        capturedCounts.Take(2).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task PollingDelay_ResetsToZeroAfterMessagesReceived()
    {
        var secondDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var capturedCounts = new List<int>();
        var pollCount = 0;
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                pollCount++;
                return pollCount == 2 ? MessagesResponse(MakeMessage()) : EmptyResponse();
            });
        var timeProvider = new FakeTimeProvider(StartTime);
        var options = new StorageQueueTriggerOptions
        {
            PollingDelay = count =>
            {
                capturedCounts.Add(count);
                if (capturedCounts.Count == 2) secondDelayStarted.TrySetResult();
                return TimeSpan.FromSeconds(1);
            }
        };
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, options, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await secondDelayStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        capturedCounts.Take(2).ShouldBe([0, 0]);
    }

    [Fact]
    public async Task WhenCancelled_Stops()
    {
        var (serviceClient, queueClient) = SubClients();
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(emptyResponse);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient);

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    // ---------------------------------------------------------------------------
    // Context
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Context_OccurredAtReflectsTimeOfReceive()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage());
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DateTimeOffset? capturedOccurredAt = null;
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient);

        var executeTask = trigger.ExecuteAsync((ctx, ct) =>
        {
            capturedOccurredAt = ctx.OccurredAt;
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        capturedOccurredAt.ShouldBe(StartTime);
    }

    [Fact]
    public async Task Context_ExposesRawQueueMessage()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage(messageId: "test-id"));
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? capturedId = null;
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient);

        var executeTask = trigger.ExecuteAsync((ctx, ct) =>
        {
            capturedId = ctx.Value.MessageId;
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        capturedId.ShouldBe("test-id");
    }

    [Fact]
    public async Task Context_DeleteMessageAsyncCallsQueueClient()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage(messageId: "del-id", popReceipt: "del-pop"));
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        queueClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, new StorageQueueTriggerOptions { AutoCompleteMessages = false });

        var executeTask = trigger.ExecuteAsync(async (ctx, ct) =>
        {
            await ctx.DeleteMessageAsync(ct);
            workerInvoked.TrySetResult();
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        await queueClient.Received(1).DeleteMessageAsync("del-id", "del-pop", Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // Error behaviour
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WhenNextThrows_DoesNotStopLoop()
    {
        var (serviceClient, queueClient) = SubClients();
        var messages = new[] { MakeMessage(messageId: "a"), MakeMessage(messageId: "b") };
        var firstResponse = MessagesResponse(messages);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var callCount = 0;
        var secondCallDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient);

        var executeTask = trigger.ExecuteAsync((_, _) =>
        {
            callCount++;
            if (callCount >= 2) secondCallDone.TrySetResult();
            throw new InvalidOperationException();
        }, cts.Token);

        await secondCallDone.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        callCount.ShouldBe(2);
    }

    [Fact]
    public async Task WhenNextThrows_DoesNotLogError()
    {
        // Worker failures are logged by WorkerService, not the trigger
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage());
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerThrew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new FakeLogger<StorageQueueTrigger>();
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultOptions, new FakeTimeProvider(StartTime), logger);

        var executeTask = trigger.ExecuteAsync((_, _) =>
        {
            workerThrew.TrySetResult();
            throw new InvalidOperationException("boom");
        }, cts.Token);

        await workerThrew.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        logger.Collector.GetSnapshot().ShouldNotContain(log => log.Level == LogLevel.Error);
    }

    [Fact]
    public async Task WhenDeserializerThrows_LogsError()
    {
        // A message that can't produce a context is a trigger problem
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage());
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var deserializerThrew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new FakeLogger<StorageQueueTrigger<string>>();
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<string>(
            serviceClient,
            QueueName,
            DefaultOptions,
            _ =>
            {
                deserializerThrew.TrySetResult();
                throw new FormatException("bad payload");
            },
            new FakeTimeProvider(StartTime),
            logger);

        var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

        await deserializerThrew.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        logger.Collector.GetSnapshot().ShouldContain(log =>
            log.Level == LogLevel.Error && log.Exception is FormatException);
    }

    [Fact]
    public async Task WhenNextThrowsOperationCancelledAndTokenCancelled_Propagates()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage());
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient);

        var executeTask = trigger.ExecuteAsync((_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, cts.Token);

        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    // ---------------------------------------------------------------------------
    // Auto-complete
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WhenAutoCompleteIsTrue_DeletesMessageAfterSuccess()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage(messageId: "m1", popReceipt: "p1"));
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        queueClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, new StorageQueueTriggerOptions { AutoCompleteMessages = true });

        var executeTask = trigger.ExecuteAsync((_, _) =>
        {
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        await queueClient.Received(1).DeleteMessageAsync("m1", "p1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAutoCompleteIsFalse_DoesNotDeleteMessageAfterSuccess()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage(messageId: "m1", popReceipt: "p1"));
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, new StorageQueueTriggerOptions { AutoCompleteMessages = false });

        var executeTask = trigger.ExecuteAsync((_, _) =>
        {
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        await queueClient.DidNotReceive().DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // MaxConcurrentCalls
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WhenMaxConcurrentCallsIsOne_DoesNotStartSecondMessageUntilFirstCompletes()
    {
        var messages = new[] { MakeMessage(messageId: "a"), MakeMessage(messageId: "b") };
        var (serviceClient, queueClient) = SubClients();
        var batchResponse = MessagesResponse(messages);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(batchResponse, emptyResponse);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, new StorageQueueTriggerOptions { MaxMessages = 2, MaxConcurrentCalls = 1 });

        var executeTask = trigger.ExecuteAsync(async (ctx, ct) =>
        {
            if (ctx.Value.MessageId == "a")
            {
                firstStarted.TrySetResult();
                await firstGate.Task;
            }
            else
            {
                secondStarted.TrySetResult();
            }
        }, cts.Token);

        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        secondStarted.Task.IsCompleted.ShouldBeFalse();

        firstGate.TrySetResult();
        await secondStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    [Fact]
    public async Task WhenMaxConcurrentCallsIsTwo_StartsSecondMessageWhileFirstIsRunning()
    {
        var messages = new[] { MakeMessage(messageId: "a"), MakeMessage(messageId: "b") };
        var (serviceClient, queueClient) = SubClients();
        var batchResponse = MessagesResponse(messages);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(batchResponse, emptyResponse);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, new StorageQueueTriggerOptions { MaxMessages = 2, MaxConcurrentCalls = 2 });

        var executeTask = trigger.ExecuteAsync(async (ctx, ct) =>
        {
            if (ctx.Value.MessageId == "a")
            {
                firstStarted.TrySetResult();
                await firstGate.Task;
            }
            else
            {
                secondStarted.TrySetResult();
            }
        }, cts.Token);

        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await secondStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        firstGate.TrySetResult();
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    // ---------------------------------------------------------------------------
    // Dead-letter threshold
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WhenDequeueCountReachesThreshold_DeadLettersMessage()
    {
        var options = new StorageQueueTriggerOptions { MaxDeliveryCount = 3 };
        var message = QueuesModelFactory.QueueMessage("m1", "p1", BinaryData.FromString("{}"), dequeueCount: 3);
        var (serviceClient, queueClient) = SubClients();
        var poisonClient = Substitute.For<QueueClient>();
        serviceClient.GetQueueClient($"{QueueName}-poison").Returns(poisonClient);
        var sendResponse = Substitute.For<Response<SendReceipt>>();
        var firstResponse = MessagesResponse(message);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        queueClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());
        var deadLetterSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        poisonClient.SendMessageAsync(Arg.Any<BinaryData>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ => { deadLetterSent.TrySetResult(); return sendResponse; });
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, options);

        var executeTask = trigger.ExecuteAsync((_, _) => throw new InvalidOperationException("fail"), cts.Token);

        await deadLetterSent.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        await poisonClient.Received(1).SendMessageAsync(Arg.Any<BinaryData>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
        await queueClient.Received(1).DeleteMessageAsync("m1", "p1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenDequeueCountBelowThreshold_DoesNotDeadLetterMessage()
    {
        var options = new StorageQueueTriggerOptions { MaxDeliveryCount = 3, MaxMessages = 2 };
        var badMessage = QueuesModelFactory.QueueMessage("m1", "p1", BinaryData.FromString("{}"), dequeueCount: 2);
        var goodMessage = MakeMessage(messageId: "m2");
        var (serviceClient, queueClient) = SubClients();
        var poisonClient = Substitute.For<QueueClient>();
        serviceClient.GetQueueClient($"{QueueName}-poison").Returns(poisonClient);
        var batchResponse = MessagesResponse(badMessage, goodMessage);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(batchResponse, emptyResponse);
        queueClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());
        var goodMessageProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, options);

        var executeTask = trigger.ExecuteAsync((ctx, _) =>
        {
            if (ctx.Value.MessageId == "m2") goodMessageProcessed.TrySetResult();
            if (ctx.Value.MessageId == "m1") throw new InvalidOperationException("fail");
            return Task.CompletedTask;
        }, cts.Token);

        await goodMessageProcessed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        await poisonClient.DidNotReceive().SendMessageAsync(Arg.Any<BinaryData>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenDeadLetterThresholdIsZero_NeverDeadLetters()
    {
        var options = new StorageQueueTriggerOptions { MaxDeliveryCount = 0, MaxMessages = 2 };
        var badMessage = QueuesModelFactory.QueueMessage("m1", "p1", BinaryData.FromString("{}"), dequeueCount: 100);
        var goodMessage = MakeMessage(messageId: "m2");
        var (serviceClient, queueClient) = SubClients();
        var poisonClient = Substitute.For<QueueClient>();
        serviceClient.GetQueueClient($"{QueueName}-poison").Returns(poisonClient);
        var batchResponse = MessagesResponse(badMessage, goodMessage);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(batchResponse, emptyResponse);
        queueClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());
        var goodMessageProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = MakeTrigger(serviceClient, options);

        var executeTask = trigger.ExecuteAsync((ctx, _) =>
        {
            if (ctx.Value.MessageId == "m2") goodMessageProcessed.TrySetResult();
            if (ctx.Value.MessageId == "m1") throw new InvalidOperationException("fail");
            return Task.CompletedTask;
        }, cts.Token);

        await goodMessageProcessed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        await poisonClient.DidNotReceive().SendMessageAsync(Arg.Any<BinaryData>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }
}

[Trait("Category", "L0")]
public class StorageQueueTriggerTypedTests
{
    private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly StorageQueueTriggerOptions DefaultOptions = new();
    private const string QueueName = "test-queue";

    private static (QueueServiceClient ServiceClient, QueueClient QueueClient) SubClients()
    {
        var queueClient = Substitute.For<QueueClient>();
        var serviceClient = Substitute.For<QueueServiceClient>();
        serviceClient.GetQueueClient(QueueName).Returns(queueClient);
        return (serviceClient, queueClient);
    }

    private static QueueMessage MakeMessage(string body = "{}", string messageId = "msg-1", string popReceipt = "pop-1") =>
        QueuesModelFactory.QueueMessage(messageId, popReceipt, BinaryData.FromString(body), 0);

    private static Response<QueueMessage[]> MessagesResponse(params QueueMessage[] messages)
    {
        var response = Substitute.For<Response<QueueMessage[]>>();
        response.Value.Returns(messages);
        return response;
    }

    private static Response<QueueMessage[]> EmptyResponse() => MessagesResponse();

    // ---------------------------------------------------------------------------
    // Constructor guards
    // ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_WhenQueueServiceClientIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(null!, QueueName, DefaultOptions, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenQueueNameIsNull_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), null!, DefaultOptions, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenQueueNameIsWhiteSpace_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), "   ", DefaultOptions, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenConfigIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, null!, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenDeserializerIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, DefaultOptions, null!, TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, DefaultOptions, StorageQueueMessageDeserializers.Json<string>(), null!, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, DefaultOptions, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, null!));

    // ---------------------------------------------------------------------------
    // Typed message delivery
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeserializedValue_IsExposedOnContext()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage("""{"Name":"hello"}"""));
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TestPayload? captured = null;
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<TestPayload>(
            serviceClient, QueueName, DefaultOptions, StorageQueueMessageDeserializers.Json<TestPayload>(),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<TestPayload>>());

        var executeTask = trigger.ExecuteAsync((ctx, ct) =>
        {
            captured = ctx.Value;
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        captured.ShouldNotBeNull();
        captured!.Name.ShouldBe("hello");
    }

    [Fact]
    public async Task RawEnvelopeMessage_IsExposedOnContext()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage("""{"Name":"hello"}""", messageId: "raw-id"));
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? capturedId = null;
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<TestPayload>(
            serviceClient, QueueName, DefaultOptions, StorageQueueMessageDeserializers.Json<TestPayload>(),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<TestPayload>>());

        var executeTask = trigger.ExecuteAsync((ctx, ct) =>
        {
            capturedId = ctx.Message.MessageId;
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        capturedId.ShouldBe("raw-id");
    }

    [Fact]
    public async Task WhenDeserializationFails_SkipsMessageAndContinuesProcessingRemainingMessages()
    {
        var badMessage = MakeMessage("not-valid-json", messageId: "bad");
        var goodMessage = MakeMessage("""{"Name":"good"}""", messageId: "good");
        var (serviceClient, queueClient) = SubClients();
        var options = new StorageQueueTriggerOptions { MaxMessages = 2 };
        var firstResponse = MessagesResponse(badMessage, goodMessage);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        queueClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveredIds = new List<string>();
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<TestPayload>(
            serviceClient, QueueName, options, StorageQueueMessageDeserializers.Json<TestPayload>(),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<TestPayload>>());

        var executeTask = trigger.ExecuteAsync((ctx, ct) =>
        {
            deliveredIds.Add(ctx.Message.MessageId);
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        deliveredIds.ShouldBe(["good"]);
    }

    [Fact]
    public async Task WhenCustomDeserializerProvided_UsesItInsteadOfJson()
    {
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(MakeMessage("custom-body"));
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? captured = null;
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<string>(
            serviceClient, QueueName, DefaultOptions,
            msg => Task.FromResult(msg.Body.ToString().ToUpper()),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<string>>());

        var executeTask = trigger.ExecuteAsync((ctx, ct) =>
        {
            captured = ctx.Value;
            workerInvoked.TrySetResult();
            return Task.CompletedTask;
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        captured.ShouldBe("CUSTOM-BODY");
    }

    private record TestPayload(string Name);
}
