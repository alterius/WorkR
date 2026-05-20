using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

[Trait("Category", "L0")]
public class StorageQueueTriggerTests
{
    private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly StorageQueueTriggerConfig DefaultConfig = new();
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
            new StorageQueueTrigger(null!, QueueName, DefaultConfig, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenQueueNameIsNull_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), null!, DefaultConfig, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenQueueNameIsWhiteSpace_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), "   ", DefaultConfig, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenConfigIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), QueueName, null!, TimeProvider.System, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), QueueName, DefaultConfig, null!, new FakeLogger<StorageQueueTrigger>()));

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger(Substitute.For<QueueServiceClient>(), QueueName, DefaultConfig, TimeProvider.System, null!));

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
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultConfig, new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger>());
        var invoked = false;

        var executeTask = trigger.Execute((ctx, ct) => { invoked = true; return Task.CompletedTask; }, cts.Token);

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
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultConfig, new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute((ctx, ct) =>
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
    public async Task WhenQueueIsEmpty_WaitsPollingIntervalBeforeRetrying()
    {
        var config = new StorageQueueTriggerConfig { PollingInterval = TimeSpan.FromSeconds(10) };
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
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, config, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

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
    public async Task WhenRequestFailedWithRetryableStatus_ContinuesPollingAfterInterval(int status)
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
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultConfig, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

        timeProvider.Advance(DefaultConfig.PollingInterval);
        await recoveredPollStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    [Fact]
    public async Task WhenHttpRequestExceptionThrown_WaitsDoublePollingIntervalBeforeRetrying()
    {
        var config = new StorageQueueTriggerConfig { PollingInterval = TimeSpan.FromSeconds(5) };
        var secondPollStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (serviceClient, queueClient) = SubClients();
        var pollCount = 0;
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                pollCount++;
                if (pollCount == 1) throw new HttpRequestException("Network error");
                if (pollCount == 2) secondPollStarted.TrySetResult();
                return EmptyResponse();
            });
        var timeProvider = new FakeTimeProvider(StartTime);
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, config, timeProvider, new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

        pollCount.ShouldBe(1);
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        secondPollStarted.Task.IsCompleted.ShouldBeFalse(); // single interval not enough — HttpRequestException uses 2x

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await secondPollStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
    }

    [Fact]
    public async Task WhenCancelled_Stops()
    {
        var (serviceClient, queueClient) = SubClients();
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(emptyResponse);
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultConfig, new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

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
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultConfig, new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute((ctx, ct) =>
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
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultConfig, new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute((ctx, ct) =>
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
        var deleteResponse = Substitute.For<Response>();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        queueClient.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(deleteResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger(serviceClient, QueueName, DefaultConfig, new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger>());

        var executeTask = trigger.Execute(async (ctx, ct) =>
        {
            await ctx.DeleteMessageAsync(ct);
            workerInvoked.TrySetResult();
        }, cts.Token);

        await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

        await queueClient.Received(1).DeleteMessageAsync("del-id", "del-pop", Arg.Any<CancellationToken>());
    }
}

[Trait("Category", "L0")]
public class StorageQueueTriggerTypedTests
{
    private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly StorageQueueTriggerConfig DefaultConfig = new();
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
            new StorageQueueTrigger<string>(null!, QueueName, DefaultConfig, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenQueueNameIsNull_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), null!, DefaultConfig, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenQueueNameIsWhiteSpace_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), "   ", DefaultConfig, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenConfigIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, null!, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenDeserializerIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, DefaultConfig, null!, TimeProvider.System, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, DefaultConfig, StorageQueueMessageDeserializers.Json<string>(), null!, new FakeLogger<StorageQueueTrigger<string>>()));

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new StorageQueueTrigger<string>(Substitute.For<QueueServiceClient>(), QueueName, DefaultConfig, StorageQueueMessageDeserializers.Json<string>(), TimeProvider.System, null!));

    // ---------------------------------------------------------------------------
    // Typed message delivery
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeserializedValue_IsExposedOnContext()
    {
        var message = MakeMessage("""{"Name":"hello"}""");
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(message);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TestPayload? captured = null;
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<TestPayload>(
            serviceClient, QueueName, DefaultConfig, StorageQueueMessageDeserializers.Json<TestPayload>(),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<TestPayload>>());

        var executeTask = trigger.Execute((ctx, ct) =>
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
        var message = MakeMessage("""{"Name":"hello"}""", messageId: "raw-id");
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(message);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? capturedId = null;
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<TestPayload>(
            serviceClient, QueueName, DefaultConfig, StorageQueueMessageDeserializers.Json<TestPayload>(),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<TestPayload>>());

        var executeTask = trigger.Execute((ctx, ct) =>
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
        var firstResponse = MessagesResponse(badMessage, goodMessage);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveredIds = new List<string>();
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<TestPayload>(
            serviceClient, QueueName, DefaultConfig, StorageQueueMessageDeserializers.Json<TestPayload>(),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<TestPayload>>());

        var executeTask = trigger.Execute((ctx, ct) =>
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
        var message = MakeMessage("custom-body");
        var (serviceClient, queueClient) = SubClients();
        var firstResponse = MessagesResponse(message);
        var emptyResponse = EmptyResponse();
        queueClient.ReceiveMessagesAsync(Arg.Any<int?>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, emptyResponse);
        var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? captured = null;
        using var cts = new CancellationTokenSource();
        var trigger = new StorageQueueTrigger<string>(
            serviceClient, QueueName, DefaultConfig,
            msg => Task.FromResult(msg.Body.ToString().ToUpper()),
            new FakeTimeProvider(StartTime), new FakeLogger<StorageQueueTrigger<string>>());

        var executeTask = trigger.Execute((ctx, ct) =>
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
