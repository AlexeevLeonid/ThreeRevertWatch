using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Gateway.Hubs;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;

namespace ThreeRevertWatch.Gateway.Workers;

public sealed class ArticleConflictBroadcastWorker : BackgroundService
{
    private readonly IKafkaConsumerLoop<string, ArticleConflictUpdateEvent> _consumer;
    private readonly IHubContext<ConflictsHub, IConflictsHubClient> _hub;
    private readonly TopicsOptions _topics;

    public ArticleConflictBroadcastWorker(
        IKafkaConsumerLoop<string, ArticleConflictUpdateEvent> consumer,
        IHubContext<ConflictsHub, IConflictsHubClient> hub,
        IOptions<TopicsOptions> topics)
    {
        _consumer = consumer;
        _hub = hub;
        _topics = topics.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _consumer.ConsumeAsync(_topics.ArticleConflictUpdates, HandleAsync, stoppingToken);

    private async Task HandleAsync(ArticleConflictUpdateEvent update, CancellationToken cancellationToken)
    {
        await _hub.Clients.Group($"conflict-topic:{update.TopicId}").ArticleConflictUpdated(update);
        await _hub.Clients.Group($"conflict-article:{update.TopicId}:{update.PageId}").ArticleConflictUpdated(update);

        if (update.Snapshot.ConflictScore >= 70)
        {
            var alert = new ConflictAlertDto(
                update.TopicId,
                update.PageId,
                update.Snapshot.Title,
                update.Snapshot.ConflictScore,
                update.Snapshot.Status,
                update.Snapshot.Evidence,
                DateTimeOffset.UtcNow);
            await _hub.Clients.Group("conflict-topics").ConflictAlert(alert);
        }
    }
}

