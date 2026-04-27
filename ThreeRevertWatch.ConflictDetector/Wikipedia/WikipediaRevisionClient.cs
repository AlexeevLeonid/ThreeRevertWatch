using System.Globalization;
using System.Text.Json;
using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.ConflictDetector.Wikipedia;

public sealed class WikipediaRevisionClient : IWikiRevisionClient
{
    private readonly HttpClient _httpClient;

    public WikipediaRevisionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RevisionDetails> GetRevisionAsync(string wiki, long revisionId, CancellationToken cancellationToken)
    {
        var url = $"{ApiBaseUrl(wiki)}/w/api.php?action=query&format=json&prop=revisions&revids={revisionId}&rvprop=ids%7Ctimestamp%7Cuser%7Ccomment%7Ctags%7Csize%7Csha1";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
        foreach (var page in pages.EnumerateObject())
        {
            var pageElement = page.Value;
            var revision = pageElement.GetProperty("revisions")[0];
            return ParseRevision(wiki, pageElement, revision);
        }

        throw new InvalidOperationException($"Revision {revisionId} was not found in {wiki}.");
    }

    public async Task<IReadOnlyList<RevisionDetails>> GetRecentRevisionsAsync(
        string wiki,
        long pageId,
        int limit,
        CancellationToken cancellationToken)
    {
        var url = $"{ApiBaseUrl(wiki)}/w/api.php?action=query&format=json&prop=revisions&pageids={pageId}&rvlimit={limit}&rvprop=ids%7Ctimestamp%7Cuser%7Ccomment%7Ctags%7Csize%7Csha1";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var revisions = new List<RevisionDetails>();
        var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
        foreach (var page in pages.EnumerateObject())
        {
            var pageElement = page.Value;
            if (!pageElement.TryGetProperty("revisions", out var revisionsElement))
            {
                continue;
            }

            revisions.AddRange(revisionsElement.EnumerateArray().Select(revision => ParseRevision(wiki, pageElement, revision)));
        }

        return revisions.OrderBy(r => r.Timestamp).ToList();
    }

    private static RevisionDetails ParseRevision(string wiki, JsonElement pageElement, JsonElement revision)
    {
        var tags = revision.TryGetProperty("tags", out var tagsElement)
            ? tagsElement.EnumerateArray().Select(tag => tag.GetString() ?? "").Where(tag => tag.Length > 0).ToArray()
            : [];

        return new RevisionDetails(
            wiki,
            pageElement.GetProperty("pageid").GetInt64(),
            pageElement.GetProperty("title").GetString() ?? "",
            revision.GetProperty("revid").GetInt64(),
            revision.TryGetProperty("parentid", out var parent) ? parent.GetInt64() : null,
            revision.TryGetProperty("user", out var user) ? user.GetString() ?? "" : "",
            revision.TryGetProperty("timestamp", out var ts)
                ? DateTimeOffset.Parse(ts.GetString() ?? "", CultureInfo.InvariantCulture)
                : DateTimeOffset.UtcNow,
            revision.TryGetProperty("comment", out var comment) ? comment.GetString() ?? "" : "",
            tags,
            revision.TryGetProperty("size", out var size) ? size.GetInt32() : null,
            revision.TryGetProperty("sha1", out var sha1) ? sha1.GetString() : null);
    }

    private static string ApiBaseUrl(string wiki)
    {
        var language = wiki.EndsWith("wiki", StringComparison.OrdinalIgnoreCase)
            ? wiki[..^4]
            : wiki;
        return $"https://{language}.wikipedia.org";
    }
}

