namespace ThreeRevertWatch.TopicMatcher.Metadata;

public interface IWikiPageMetadataClient
{
    Task<WikiPageMetadata> GetAsync(
        string wiki,
        long pageId,
        string title,
        CancellationToken cancellationToken);
}

public sealed record WikiPageMetadata(
    string Wiki,
    long PageId,
    string Title,
    IReadOnlyList<string> Categories);
