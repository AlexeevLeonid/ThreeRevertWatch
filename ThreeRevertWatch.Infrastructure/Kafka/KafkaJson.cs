using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreeRevertWatch.Infrastructure.Kafka;

public static class KafkaJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}

