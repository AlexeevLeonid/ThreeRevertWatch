namespace ThreeRevertWatch.ConflictDetector.State;

public interface IArticleStateStore
{
    Task<ArticleRuntimeState> GetOrCreateAsync(
        string topicId,
        string wiki,
        long pageId,
        string title,
        double topicRelevanceScore,
        CancellationToken cancellationToken);

    Task SaveAsync(ArticleRuntimeState state, CancellationToken cancellationToken);
}

