using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace ThreeRevertWatch.Infrastructure.Kafka;

public sealed class KafkaJsonSerializer<T> : ISerializer<T>, IDeserializer<T>
{
    public byte[] Serialize(T data, SerializationContext context)
        => data is null ? [] : JsonSerializer.SerializeToUtf8Bytes(data, KafkaJson.Options);

    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull)
        {
            throw new InvalidOperationException($"Kafka payload for {typeof(T).Name} was null.");
        }

        return JsonSerializer.Deserialize<T>(data, KafkaJson.Options)
            ?? throw new JsonException($"Kafka payload for {typeof(T).Name} could not be deserialized: {Encoding.UTF8.GetString(data)}");
    }
}

