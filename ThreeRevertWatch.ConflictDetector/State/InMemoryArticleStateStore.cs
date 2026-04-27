using System.Collections.Concurrent;

namespace ThreeRevertWatch.ConflictDetector.State;

public sealed class InMemoryArticleStateStore : IArticleStateStore
{
    private readonly ConcurrentDictionary<string, ArticleRuntimeState> _states = new();

    public Task<ArticleRuntimeState> GetOrCreateAsync(
        string topicId,
        string wiki,
        long pageId,
        string title,
        double topicRelevanceScore,
        CancellationToken cancellationToken)
    {
        var state = _states.GetOrAdd(
            Key(topicId, wiki, pageId),
            _ => new ArticleRuntimeState(topicId, wiki, pageId, title, topicRelevanceScore));
        return Task.FromResult(state);
    }

    public Task SaveAsync(ArticleRuntimeState state, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static string Key(string topicId, string wiki, long pageId) => $"{topicId}:{wiki}:{pageId}";
}

