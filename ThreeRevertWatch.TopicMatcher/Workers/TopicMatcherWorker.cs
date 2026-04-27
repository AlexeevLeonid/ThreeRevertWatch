using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.TopicMatcher.Configuration;
using ThreeRevertWatch.TopicMatcher.Matching;
using ThreeRevertWatch.TopicMatcher.Persistence;

namespace ThreeRevertWatch.TopicMatcher.Workers;

public sealed class TopicMatcherWorker : BackgroundService
{
    private readonly IKafkaConsumerLoop<string, RawEditEvent> _consumer;
    private readonly IKafkaProducer<string, TopicMatchedEditEvent> _producer;
    private readonly ITopicMatcher _topicMatcher;
    private readonly ITopicArticleRepository _repository;
    private readonly TopicsOptions _topics;
    private readonly ConflictTopicsOptions _options;
    private readonly ILogger<TopicMatcherWorker> _logger;

    public TopicMatcherWorker(
        IKafkaConsumerLoop<string, RawEditEvent> consumer,
        IKafkaProducer<string, TopicMatchedEditEvent> producer,
        ITopicMatcher topicMatcher,
        ITopicArticleRepository repository,
        IOptions<TopicsOptions> topics,
        IOptions<ConflictTopicsOptions> options,
        ILogger<TopicMatcherWorker> logger)
    {
        _consumer = consumer;
        _producer = producer;
        _topicMatcher = topicMatcher;
        _repository = repository;
        _topics = topics.Value;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _consumer.ConsumeAsync(_topics.RawEdits, HandleAsync, stoppingToken);

    private async Task HandleAsync(RawEditEvent edit, CancellationToken cancellationToken)
    {
        var matches = await _topicMatcher.MatchAsync(edit, cancellationToken);
        _logger.LogInformation(
            "Metric={Metric} Wiki={Wiki} PageId={PageId} RevisionId={RevisionId} MatchCount={MatchCount}",
            "topic_matcher_event_processed",
            edit.Wiki,
            edit.PageId,
            edit.RevisionId,
            matches.Count);

        foreach (var match in matches)
        {
            var now = DateTimeOffset.UtcNow;
            await _repository.UpsertAsync(new TopicArticleMembershipDto(
                match.TopicId,
                edit.Wiki,
                edit.PageId,
                edit.Title,
                match.Status,
                match.Confidence,
                match.Reasons,
                now,
                now), cancellationToken);

            _logger.LogInformation(
                "Metric={Metric} TopicId={TopicId} Wiki={Wiki} PageId={PageId} RevisionId={RevisionId} RelevanceScore={RelevanceScore} Status={Status}",
                "topic_matcher_match_found",
                match.TopicId,
                edit.Wiki,
                edit.PageId,
                edit.RevisionId,
                match.Confidence,
                match.Status);

            if (match.Confidence < _options.DetectorPublishThreshold)
            {
                continue;
            }

            var matched = new TopicMatchedEditEvent(
                Guid.NewGuid().ToString("n"),
                match.TopicId,
                match.Confidence,
                match.Reasons,
                edit,
                now);

            await _producer.ProduceAsync(
                _topics.TopicMatchedEdits,
                edit.PageId.ToString(),
                matched,
                cancellationToken);
        }
    }
}

