using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Infrastructure.Configuration;

namespace ThreeRevertWatch.Gateway.Services;

public sealed class PipelineMetricsService
{
    private static readonly TimeSpan KafkaTimeout = TimeSpan.FromSeconds(5);

    private readonly KafkaSettings _kafka;
    private readonly TopicsOptions _topics;
    private readonly ILogger<PipelineMetricsService> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, StageSample> _previousSamples = [];

    public PipelineMetricsService(
        IOptions<KafkaSettings> kafka,
        IOptions<TopicsOptions> topics,
        ILogger<PipelineMetricsService> logger)
    {
        _kafka = kafka.Value;
        _topics = topics.Value;
        _logger = logger;
    }

    public async Task<PipelineMetricsDto> GetAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var stages = new List<PipelineStageMetricsDto>();

        foreach (var stage in BuildStages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(await Task.Run(() => ReadStage(stage, now), cancellationToken));
        }

        return new PipelineMetricsDto(now, stages.Sum(stage => stage.Lag), stages);
    }

    private PipelineStageMetricsDto ReadStage(StageDefinition stage, DateTimeOffset now)
    {
        try
        {
            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = _kafka.BootstrapServers,
                GroupId = stage.ConsumerGroup ?? "threerevertwatch-pipeline-metrics",
                EnableAutoCommit = false,
                AllowAutoCreateTopics = false,
                ClientId = $"pipeline-metrics-{stage.Id}"
            }).Build();
            using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _kafka.BootstrapServers,
                ClientId = $"pipeline-metrics-admin-{stage.Id}"
            }).Build();

            var partitions = GetPartitions(admin, stage.Topic);
            var committed = stage.ConsumerGroup is null
                ? []
                : consumer.Committed(partitions, KafkaTimeout).ToDictionary(
                    offset => offset.TopicPartition,
                    offset => offset.Offset);

            long currentOffset = 0;
            long logEndOffset = 0;
            long lag = 0;

            foreach (var partition in partitions)
            {
                var watermark = consumer.QueryWatermarkOffsets(partition, KafkaTimeout);
                var high = Math.Max(0, watermark.High.Value);
                var current = stage.ConsumerGroup is null
                    ? high
                    : CommittedOffset(committed.GetValueOrDefault(partition));

                currentOffset += current;
                logEndOffset += high;
                lag += Math.Max(0, high - current);
            }

            var throughputOffset = stage.ConsumerGroup is null ? logEndOffset : currentOffset;
            var messagesPerMinute = CalculateRate(stage.Id, throughputOffset, now);

            return new PipelineStageMetricsDto(
                stage.Id,
                stage.Name,
                stage.Service,
                stage.Topic,
                stage.ConsumerGroup,
                currentOffset,
                logEndOffset,
                lag,
                Math.Round(messagesPerMinute, 1),
                StatusFor(lag));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pipeline metrics stage failed StageId={StageId} Topic={Topic}", stage.Id, stage.Topic);
            return new PipelineStageMetricsDto(
                stage.Id,
                stage.Name,
                stage.Service,
                stage.Topic,
                stage.ConsumerGroup,
                0,
                0,
                0,
                0,
                "unavailable");
        }
    }

    private double CalculateRate(string stageId, long offset, DateTimeOffset now)
    {
        lock (_lock)
        {
            if (!_previousSamples.TryGetValue(stageId, out var previous))
            {
                _previousSamples[stageId] = new StageSample(offset, now);
                return 0;
            }

            _previousSamples[stageId] = new StageSample(offset, now);

            var minutes = Math.Max((now - previous.SampledAt).TotalMinutes, 0.001);
            var delta = Math.Max(0, offset - previous.Offset);
            return delta / minutes;
        }
    }

    private static IReadOnlyList<TopicPartition> GetPartitions(IAdminClient admin, string topic)
    {
        var metadata = admin.GetMetadata(topic, KafkaTimeout);
        var topicMetadata = metadata.Topics.FirstOrDefault(item => string.Equals(item.Topic, topic, StringComparison.Ordinal));
        if (topicMetadata is null || topicMetadata.Error.IsError)
        {
            throw new InvalidOperationException($"Kafka topic metadata is unavailable for {topic}.");
        }

        return topicMetadata.Partitions
            .Select(partition => new TopicPartition(topic, new Partition(partition.PartitionId)))
            .ToArray();
    }

    private static long CommittedOffset(Offset offset)
        => offset == Offset.Unset ? 0 : Math.Max(0, offset.Value);

    private static string StatusFor(long lag)
        => lag switch
        {
            0 => "healthy",
            <= 100 => "catching_up",
            _ => "backlog"
        };

    private IReadOnlyList<StageDefinition> BuildStages()
        =>
        [
            new("collector", "Collected edits", "Collector", _topics.RawEdits, null),
            new("topicmatcher", "Raw edits consumed", "TopicMatcher", _topics.RawEdits, "threerevertwatch-topicmatcher"),
            new("conflictdetector", "Topic matches consumed", "ConflictDetector", _topics.TopicMatchedEdits, "threerevertwatch-conflictdetector"),
            new("aggregator", "Article updates consumed", "Aggregator", _topics.ArticleConflictUpdates, "threerevertwatch-aggregator"),
            new("gateway-articles", "Article broadcasts consumed", "Gateway", _topics.ArticleConflictUpdates, "threerevertwatch-gateway"),
            new("gateway-topics", "Topic broadcasts consumed", "Gateway", _topics.TopicConflictUpdates, "threerevertwatch-gateway")
        ];

    private sealed record StageDefinition(
        string Id,
        string Name,
        string Service,
        string Topic,
        string? ConsumerGroup);

    private sealed record StageSample(long Offset, DateTimeOffset SampledAt);
}
