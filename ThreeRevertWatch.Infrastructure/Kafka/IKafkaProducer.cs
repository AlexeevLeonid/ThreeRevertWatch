using Confluent.Kafka;

namespace ThreeRevertWatch.Infrastructure.Kafka;

public interface IKafkaProducer<TKey, TValue>
{
    Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        TKey key,
        TValue value,
        CancellationToken cancellationToken = default);
}

