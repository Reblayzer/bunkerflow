using System.Text.Json;
using System.Text.Json.Serialization;

namespace BunkerFlow.Contracts;

/// <summary>
/// One serializer configuration shared by every hop (Kafka, Service Bus, the
/// API, the landing store) so a message written by one component is always
/// readable by the next.
/// </summary>
public static class EventSerialization
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(IntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, Options);

    public static IntegrationEvent? Deserialize(string json) =>
        JsonSerializer.Deserialize<IntegrationEvent>(json, Options);
}
