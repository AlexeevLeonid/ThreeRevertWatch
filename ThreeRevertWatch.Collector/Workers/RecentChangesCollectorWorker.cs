using Microsoft.Extensions.Options;
using ThreeRevertWatch.Collector.Configuration;
using ThreeRevertWatch.Collector.Wikipedia;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;

namespace ThreeRevertWatch.Collector.Workers;

public sealed class RecentChangesCollectorWorker : BackgroundService
{
    private readonly IRecentChangesClient _client;
    private readonly IKafkaProducer<string, RawEditEvent> _producer;
    private readonly CollectorOptions _collectorOptions;
    private readonly TopicsOptions _topics;
    private readonly ILogger<RecentChangesCollectorWorker> _logger;
    private readonly Queue<long> _seenOrder = new();
    private readonly HashSet<long> _seen = [];

    public RecentChangesCollectorWorker(
        IRecentChangesClient client,
        IKafkaProducer<string, RawEditEvent> producer,
        IOptions<CollectorOptions> collectorOptions,
        IOptions<TopicsOptions> topics,
        ILogger<RecentChangesCollectorWorker> logger)
    {
        _client = client;
        _producer = producer;
        _collectorOptions = collectorOptions.Value;
        _topics = topics.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var wiki in _collectorOptions.Wikis)
            {
                await PollWikiAsync(wiki, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(_collectorOptions.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task PollWikiAsync(string wiki, CancellationToken cancellationToken)
    {
        try
        {
            var edits = await _client.GetRecentChangesAsync(
                wiki,
                _collectorOptions.RecentChangesLimit,
                _collectorOptions.AllowedNamespaces,
                cancellationToken);

            foreach (var edit in edits)
            {
                if (!Remember(edit.WikiEditId))
                {
                    continue;
                }

                await _producer.ProduceAsync(_topics.RawEdits, edit.PageId.ToString(), edit, cancellationToken);
                _logger.LogInformation(
                    "Metric={Metric} Wiki={Wiki} PageId={PageId} RevisionId={RevisionId} Title={Title}",
                    "collector_raw_edit_published",
                    edit.Wiki,
                    edit.PageId,
                    edit.RevisionId,
                    edit.Title);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecentChanges poll failed Wiki={Wiki}", wiki);
        }
    }

    private bool Remember(long wikiEditId)
    {
        if (!_seen.Add(wikiEditId))
        {
            return false;
        }

        _seenOrder.Enqueue(wikiEditId);
        while (_seenOrder.Count > 2_000)
        {
            _seen.Remove(_seenOrder.Dequeue());
        }

        return true;
    }
}

