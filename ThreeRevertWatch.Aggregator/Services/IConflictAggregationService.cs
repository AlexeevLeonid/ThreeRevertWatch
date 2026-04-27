using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Aggregator.Services;

public interface IConflictAggregationService
{
    Task<TopicSnapshotDto> ApplyArticleUpdateAsync(
        ArticleConflictUpdateEvent update,
        CancellationToken cancellationToken);
}

