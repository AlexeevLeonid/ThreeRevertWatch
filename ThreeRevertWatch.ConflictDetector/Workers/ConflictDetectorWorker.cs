using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.Classification;
using ThreeRevertWatch.ConflictDetector.Configuration;
using ThreeRevertWatch.ConflictDetector.Persistence;
using ThreeRevertWatch.ConflictDetector.Scoring;
using ThreeRevertWatch.ConflictDetector.State;
using ThreeRevertWatch.ConflictDetector.Wikipedia;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;

namespace ThreeRevertWatch.ConflictDetector.Workers;

public sealed class ConflictDetectorWorker : BackgroundService
{
    private readonly IKafkaConsumerLoop<string, TopicMatchedEditEvent> _consumer;
    private readonly IKafkaProducer<string, ArticleConflictUpdateEvent> _producer;
    private readonly IWikiRevisionClient _revisionClient;
    private readonly IArticleStateStore _stateStore;
    private readonly IEditClassifier _classifier;
    private readonly IArticleConflictScorer _scorer;
    private readonly IArticleConflictRepository _repository;
    private readonly TopicsOptions _topics;
    private readonly ConflictDetectorOptions _options;
    private readonly ILogger<ConflictDetectorWorker> _logger;

    public ConflictDetectorWorker(
        IKafkaConsumerLoop<string, TopicMatchedEditEvent> consumer,
        IKafkaProducer<string, ArticleConflictUpdateEvent> producer,
        IWikiRevisionClient revisionClient,
        IArticleStateStore stateStore,
        IEditClassifier classifier,
        IArticleConflictScorer scorer,
        IArticleConflictRepository repository,
        IOptions<TopicsOptions> topics,
        IOptions<ConflictDetectorOptions> options,
        ILogger<ConflictDetectorWorker> logger)
    {
        _consumer = consumer;
        _producer = producer;
        _revisionClient = revisionClient;
        _stateStore = stateStore;
        _classifier = classifier;
        _scorer = scorer;
        _repository = repository;
        _topics = topics.Value;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _consumer.ConsumeAsync(_topics.TopicMatchedEdits, HandleAsync, stoppingToken);

    private async Task HandleAsync(TopicMatchedEditEvent matched, CancellationToken cancellationToken)
    {
        var raw = matched.RawEdit;
        var revision = await ResolveRevisionAsync(matched, cancellationToken);
        var state = await _stateStore.GetOrCreateAsync(
            matched.TopicId,
            revision.Wiki,
            revision.PageId,
            revision.Title,
            matched.TopicRelevanceScore,
            cancellationToken);

        var edit = await _classifier.ClassifyAsync(matched, revision, state, cancellationToken);
        var newEdges = state.Apply(revision, edit, _options.RecentRevisionWindowSize, _options.RecentEditWindowSize);
        var snapshot = _scorer.BuildSnapshot(state, edit);
        state.CurrentSnapshot = snapshot;
        await _stateStore.SaveAsync(state, cancellationToken);
        await _repository.SaveAsync(edit, snapshot, newEdges, cancellationToken);

        var update = new ArticleConflictUpdateEvent(
            Guid.NewGuid().ToString("n"),
            matched.TopicId,
            revision.PageId,
            edit,
            snapshot,
            DateTimeOffset.UtcNow);

        await _producer.ProduceAsync(
            _topics.ArticleConflictUpdates,
            $"{matched.TopicId}:{revision.PageId}",
            update,
            cancellationToken);

        _logger.LogInformation(
            "Metric={Metric} TopicId={TopicId} Wiki={Wiki} PageId={PageId} RevisionId={RevisionId} ActionType={ActionType} ConflictScore={ConflictScore} Status={Status}",
            "conflict_detector_event_processed",
            matched.TopicId,
            revision.Wiki,
            revision.PageId,
            revision.RevisionId,
            edit.Action,
            snapshot.ConflictScore,
            snapshot.Status);

        if (edit.Action is EditActionType.ExactRevert or EditActionType.VandalismCleanup or EditActionType.SpamCleanup or EditActionType.CopyvioCleanup)
        {
            _logger.LogInformation(
                "Metric={Metric} TopicId={TopicId} Wiki={Wiki} PageId={PageId} RevisionId={RevisionId} RevertedUsers={RevertedUsers}",
                "conflict_detector_exact_revert_detected",
                matched.TopicId,
                revision.Wiki,
                revision.PageId,
                revision.RevisionId,
                string.Join(",", edit.RevertedUsers));
        }

        _logger.LogInformation(
            "Metric={Metric} TopicId={TopicId} Wiki={Wiki} PageId={PageId} RevisionId={RevisionId} ConflictScore={ConflictScore} Status={Status}",
            "conflict_detector_article_score",
            matched.TopicId,
            revision.Wiki,
            revision.PageId,
            revision.RevisionId,
            snapshot.ConflictScore,
            snapshot.Status);

        _ = raw;
    }

    private async Task<RevisionDetails> ResolveRevisionAsync(TopicMatchedEditEvent matched, CancellationToken cancellationToken)
    {
        var raw = matched.RawEdit;
        if (raw.RevisionId is > 0)
        {
            try
            {
                return await _revisionClient.GetRevisionAsync(raw.Wiki, raw.RevisionId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Revision API failed, falling back to raw edit. Wiki={Wiki} RevisionId={RevisionId}", raw.Wiki, raw.RevisionId);
            }
        }

        return new RevisionDetails(
            raw.Wiki,
            raw.PageId,
            raw.Title,
            raw.RevisionId ?? raw.WikiEditId,
            raw.ParentRevisionId,
            raw.User,
            raw.Timestamp,
            raw.Comment,
            raw.Tags,
            raw.NewLength,
            null);
    }
}

