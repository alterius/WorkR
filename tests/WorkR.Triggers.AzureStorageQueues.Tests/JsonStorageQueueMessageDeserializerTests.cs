using System.Text.Json;
using Azure.Storage.Queues.Models;
using Shouldly;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

[Trait("Category", "L0")]
public class JsonStorageQueueMessageDeserializerTests
{
    private static QueueMessage MakeMessage(string body) =>
        QueuesModelFactory.QueueMessage("id", "pop", BinaryData.FromString(body), 0);

    [Fact]
    public async Task Deserialize_ValidJson_ReturnsDeserializedValue()
    {
        var deserializer = new JsonStorageQueueMessageDeserializer<Payload>();

        var result = await deserializer.Deserialize(MakeMessage("""{"Name":"hello"}"""));

        result.Name.ShouldBe("hello");
    }

    [Fact]
    public async Task Deserialize_WithCustomOptions_HonoursOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var deserializer = new JsonStorageQueueMessageDeserializer<Payload>(options);

        var result = await deserializer.Deserialize(MakeMessage("""{"name":"hello"}"""));

        result.Name.ShouldBe("hello");
    }

    [Fact]
    public async Task Deserialize_InvalidJson_Throws()
    {
        var deserializer = new JsonStorageQueueMessageDeserializer<Payload>();

        await Should.ThrowAsync<JsonException>(() =>
            deserializer.Deserialize(MakeMessage("not-valid-json")));
    }

    [Fact]
    public async Task Deserialize_JsonNull_ThrowsInvalidOperationException()
    {
        var deserializer = new JsonStorageQueueMessageDeserializer<Payload>();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            deserializer.Deserialize(MakeMessage("null")));
    }

    private record Payload(string Name);
}
