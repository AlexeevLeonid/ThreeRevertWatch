using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Infrastructure.Configuration;

namespace ThreeRevertWatch.Infrastructure.Kafka;

public sealed class KafkaProducer<TKey, TValue> : IKafkaProducer<TKey, TValue>, IDisposable
{
    private readonly IProducer<TKey, TValue> _producer;
    private readonly ILogger<KafkaProducer<TKey, TValue>> _logger;

    public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer<TKey, TValue>> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MaxInFlight = 5
        };

        var builder = new ProducerBuilder<TKey, TValue>(config)
            .SetValueSerializer(new KafkaJsonSerializer<TValue>());

        if (typeof(TKey) != typeof(string))
        {
            builder.SetKeySerializer(new KafkaJsonSerializer<TKey>());
        }

        _producer = builder.Build();
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        TKey key,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        var result = await _producer.ProduceAsync(topic, new Message<TKey, TValue>
        {
            Key = key,
            Value = value
        }, cancellationToken);

        _logger.LogDebug(
            "Kafka message delivered Topic={Topic} Partition={Partition} Offset={Offset}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value);

        return result;
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}

