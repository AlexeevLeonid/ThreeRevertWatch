namespace ThreeRevertWatch.TopicMatcher.Metadata;

public sealed class NoopWikiPageMetadataClient : IWikiPageMetadataClient
{
    public Task<WikiPageMetadata> GetAsync(
        string wiki,
        long pageId,
        string title,
        CancellationToken cancellationToken)
        => Task.FromResult(new WikiPageMetadata(wiki, pageId, title, []));
}
