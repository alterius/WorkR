using System.Text.Json;
using Azure.Storage.Queues.Models;
using Shouldly;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

[Trait("Category", "L0")]
public class StorageQueueMessageDeserializersTests
{
    [Fact]
    public async Task Json_ValidJson_ReturnsDeserializedValue()
    {
        var deserializer = StorageQueueMessageDeserializers.Json<Payload>();
        var result = await deserializer(MakeMessage("""{"Name":"hello"}"""));

        result.Name.ShouldBe("hello");
    }

    [Fact]
    public async Task Json_WithCustomOptions_HonoursOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var deserializer = StorageQueueMessageDeserializers.Json<Payload>(options);

        var result = await deserializer(MakeMessage("""{"name":"hello"}"""));

        result.Name.ShouldBe("hello");
    }

    [Fact]
    public async Task Json_InvalidJson_Throws()
    {
        var deserializer = StorageQueueMessageDeserializers.Json<Payload>();

        await Should.ThrowAsync<JsonException>(() =>
            deserializer(MakeMessage("not-valid-json")));
    }

    [Fact]
    public async Task Json_JsonNull_ThrowsJsonException()
    {
        var deserializer = StorageQueueMessageDeserializers.Json<Payload>();

        await Should.ThrowAsync<JsonException>(() =>
            deserializer(MakeMessage("null")));
    }

    private static QueueMessage MakeMessage(string body) =>
        QueuesModelFactory.QueueMessage("id", "pop", BinaryData.FromString(body), 0);

    private record Payload(string Name);
}
