using Microsoft.Extensions.Options;
using ThreeRevertWatch.Aggregator.Services;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;

namespace ThreeRevertWatch.Aggregator.Workers;

public sealed class ArticleConflictUpdateWorker : BackgroundService
{
    private readonly IKafkaConsumerLoop<string, ArticleConflictUpdateEvent> _consumer;
    private readonly IKafkaProducer<string, TopicConflictUpdateEvent> _producer;
    private readonly IConflictAggregationService _aggregationService;
    private readonly TopicsOptions _topics;
    private readonly ILogger<ArticleConflictUpdateWorker> _logger;

    public ArticleConflictUpdateWorker(
        IKafkaConsumerLoop<string, ArticleConflictUpdateEvent> consumer,
        IKafkaProducer<string, TopicConflictUpdateEvent> producer,
        IConflictAggregationService aggregationService,
        IOptions<TopicsOptions> topics,
        ILogger<ArticleConflictUpdateWorker> logger)
    {
        _consumer = consumer;
        _producer = producer;
        _aggregationService = aggregationService;
        _topics = topics.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _consumer.ConsumeAsync(_topics.ArticleConflictUpdates, HandleAsync, stoppingToken);

    private async Task HandleAsync(ArticleConflictUpdateEvent update, CancellationToken cancellationToken)
    {
        var topic = await _aggregationService.ApplyArticleUpdateAsync(update, cancellationToken);
        await _producer.ProduceAsync(
            _topics.TopicConflictUpdates,
            topic.TopicId,
            new TopicConflictUpdateEvent(Guid.NewGuid().ToString("n"), topic, DateTimeOffset.UtcNow),
            cancellationToken);

        _logger.LogDebug(
            "Article update aggregated TopicId={TopicId} PageId={PageId} Score={Score}",
            update.TopicId,
            update.PageId,
            update.Snapshot.ConflictScore);
    }
}

