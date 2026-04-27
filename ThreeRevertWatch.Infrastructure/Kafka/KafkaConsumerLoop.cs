using System.Diagnostics;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Infrastructure.Configuration;

namespace ThreeRevertWatch.Infrastructure.Kafka;

public interface IKafkaConsumerLoop<TKey, TValue>
{
    Task ConsumeAsync(
        string topic,
        Func<TValue, CancellationToken, Task> handler,
        CancellationToken cancellationToken);
}

public sealed class KafkaConsumerLoop<TKey, TValue> : IKafkaConsumerLoop<TKey, TValue>
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaConsumerLoop<TKey, TValue>> _logger;

    public KafkaConsumerLoop(IOptions<KafkaSettings> settings, ILogger<KafkaConsumerLoop<TKey, TValue>> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ConsumeAsync(
        string topic,
        Func<TValue, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.GroupId,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_settings.AutoOffsetReset, ignoreCase: true),
            EnableAutoCommit = _settings.EnableAutoCommit,
            SessionTimeoutMs = _settings.SessionTimeoutMs,
            MaxPollIntervalMs = _settings.MaxPollIntervalMs
        };

        var builder = new ConsumerBuilder<TKey, TValue>(config)
            .SetValueDeserializer(new KafkaJsonSerializer<TValue>())
            .SetErrorHandler((_, error) =>
                _logger.LogError("Kafka consumer error Code={Code} Reason={Reason}", error.Code, error.Reason));

        if (typeof(TKey) != typeof(string))
        {
            builder.SetKeyDeserializer(new KafkaJsonSerializer<TKey>());
        }

        using var consumer = builder.Build();
        consumer.Subscribe(topic);
        _logger.LogInformation("Kafka consumer subscribed Topic={Topic} GroupId={GroupId}", topic, _settings.GroupId);

        var interval = Stopwatch.StartNew();
        long processed = 0;
        long slow = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<TKey, TValue>? result = null;
            try
            {
                result = consumer.Consume(TimeSpan.FromMilliseconds(250));
                if (result is null || result.IsPartitionEOF)
                {
                    continue;
                }

                var sw = Stopwatch.StartNew();
                await handler(result.Message.Value, cancellationToken);
                sw.Stop();

                processed++;
                if (sw.ElapsedMilliseconds >= _settings.SlowMessageThresholdMs)
                {
                    slow++;
                    _logger.LogWarning(
                        "Metric={Metric} Topic={Topic} Partition={Partition} Offset={Offset} ProcessingMs={ProcessingMs}",
                        "kafka_message_slow",
                        topic,
                        result.Partition.Value,
                        result.Offset.Value,
                        sw.ElapsedMilliseconds);
                }

                if (!_settings.EnableAutoCommit)
                {
                    consumer.Commit(result);
                }

                if (_settings.MetricsEnabled && interval.Elapsed >= TimeSpan.FromSeconds(_settings.MetricsLogIntervalSeconds))
                {
                    _logger.LogInformation(
                        "Metric={Metric} Topic={Topic} GroupId={GroupId} Processed={Processed} SlowCount={SlowCount}",
                        "kafka_consumer_stats",
                        topic,
                        _settings.GroupId,
                        processed,
                        slow);
                    interval.Restart();
                    processed = 0;
                    slow = 0;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Kafka consume failed Topic={Topic} Partition={Partition} Offset={Offset}",
                    topic,
                    result?.Partition.Value,
                    result?.Offset.Value);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        consumer.Close();
    }
}

