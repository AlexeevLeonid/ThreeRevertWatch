using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.ConflictDetector.Wikipedia;

public interface IWikiRevisionClient
{
    Task<RevisionDetails> GetRevisionAsync(string wiki, long revisionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RevisionDetails>> GetRecentRevisionsAsync(
        string wiki,
        long pageId,
        int limit,
        CancellationToken cancellationToken);
}

