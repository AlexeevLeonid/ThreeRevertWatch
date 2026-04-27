using System.Globalization;
using System.Text.Json;
using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Collector.Wikipedia;

public sealed class WikipediaRecentChangesClient : IRecentChangesClient
{
    private readonly HttpClient _httpClient;

    public WikipediaRecentChangesClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<RawEditEvent>> GetRecentChangesAsync(
        string wiki,
        int limit,
        IReadOnlyList<int> allowedNamespaces,
        CancellationToken cancellationToken)
    {
        var namespaces = string.Join('|', allowedNamespaces);
        var url = $"{ApiBaseUrl(wiki)}/w/api.php?action=query&format=json&list=recentchanges&rcnamespace={namespaces}&rctype=edit%7Cnew&rcprop=ids%7Ctitle%7Ctimestamp%7Cuser%7Ccomment%7Csizes%7Cflags%7Ctags&rcshow=!anon&rclimit={limit}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("query", out var query)
            || !query.TryGetProperty("recentchanges", out var recentChanges))
        {
            return [];
        }

        var collectedAt = DateTimeOffset.UtcNow;
        return recentChanges.EnumerateArray()
            .Select(change => Map(wiki, change, collectedAt))
            .Where(edit => edit.PageId > 0 && edit.RevisionId is > 0)
            .OrderBy(edit => edit.Timestamp)
            .ToList();
    }

    private static RawEditEvent Map(string wiki, JsonElement change, DateTimeOffset collectedAt)
    {
        var tags = change.TryGetProperty("tags", out var tagsElement)
            ? tagsElement.EnumerateArray().Select(tag => tag.GetString() ?? "").Where(tag => tag.Length > 0).ToArray()
            : [];

        var newLength = change.TryGetProperty("newlen", out var newLen) ? newLen.GetInt32() : 0;
        var oldLength = change.TryGetProperty("oldlen", out var oldLen) ? oldLen.GetInt32() : 0;
        var type = change.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "" : "";

        return new RawEditEvent(
            Guid.NewGuid().ToString("n"),
            change.GetProperty("rcid").GetInt64(),
            change.GetProperty("pageid").GetInt64(),
            change.GetProperty("title").GetString() ?? "",
            wiki,
            change.TryGetProperty("revid", out var revId) ? revId.GetInt64() : null,
            change.TryGetProperty("old_revid", out var oldRevId) ? oldRevId.GetInt64() : null,
            change.TryGetProperty("user", out var user) ? user.GetString() ?? "" : "",
            change.TryGetProperty("comment", out var comment) ? comment.GetString() ?? "" : "",
            tags,
            change.TryGetProperty("bot", out _),
            change.TryGetProperty("minor", out _),
            string.Equals(type, "new", StringComparison.OrdinalIgnoreCase),
            oldLength,
            newLength,
            DateTimeOffset.Parse(change.GetProperty("timestamp").GetString() ?? "", CultureInfo.InvariantCulture),
            collectedAt);
    }

    private static string ApiBaseUrl(string wiki)
    {
        var language = wiki.EndsWith("wiki", StringComparison.OrdinalIgnoreCase)
            ? wiki[..^4]
            : wiki;
        return $"https://{language}.wikipedia.org";
    }
}

