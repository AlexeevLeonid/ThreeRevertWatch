using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Gateway.Hubs;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;

namespace ThreeRevertWatch.Gateway.Workers;

public sealed class TopicConflictBroadcastWorker : BackgroundService
{
    private readonly IKafkaConsumerLoop<string, TopicConflictUpdateEvent> _consumer;
    private readonly IHubContext<ConflictsHub, IConflictsHubClient> _hub;
    private readonly TopicsOptions _topics;

    public TopicConflictBroadcastWorker(
        IKafkaConsumerLoop<string, TopicConflictUpdateEvent> consumer,
        IHubContext<ConflictsHub, IConflictsHubClient> hub,
        IOptions<TopicsOptions> topics)
    {
        _consumer = consumer;
        _hub = hub;
        _topics = topics.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _consumer.ConsumeAsync(_topics.TopicConflictUpdates, HandleAsync, stoppingToken);

    private async Task HandleAsync(TopicConflictUpdateEvent update, CancellationToken cancellationToken)
    {
        await _hub.Clients.Group("conflict-topics").TopicSnapshotUpdated(CompactTopic(update.Snapshot, 4));
        await _hub.Clients.Group($"conflict-topic:{update.Snapshot.TopicId}").TopicSnapshotUpdated(CompactTopic(update.Snapshot, 0));
    }

    private static TopicSnapshotDto CompactTopic(TopicSnapshotDto topic, int articleLimit)
        => topic with { Articles = topic.Articles.Take(articleLimit).ToList() };
}
