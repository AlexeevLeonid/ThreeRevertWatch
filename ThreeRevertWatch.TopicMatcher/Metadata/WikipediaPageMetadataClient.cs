using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.TopicMatcher.Configuration;

namespace ThreeRevertWatch.TopicMatcher.Metadata;

public sealed class WikipediaPageMetadataClient : IWikiPageMetadataClient
{
    private readonly HttpClient _httpClient;
    private readonly PageMetadataOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public WikipediaPageMetadataClient(HttpClient httpClient, IOptions<PageMetadataOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<WikiPageMetadata> GetAsync(
        string wiki,
        long pageId,
        string title,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || pageId <= 0)
        {
            return new WikiPageMetadata(wiki, pageId, title, []);
        }

        var key = $"{wiki}:{pageId}";
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Metadata;
        }

        var metadata = await FetchAsync(wiki, pageId, title, cancellationToken);
        _cache[key] = new CacheEntry(
            metadata,
            DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.CacheTtlMinutes)));
        return metadata;
    }

    private async Task<WikiPageMetadata> FetchAsync(
        string wiki,
        long pageId,
        string title,
        CancellationToken cancellationToken)
    {
        var url = $"{ApiBaseUrl(wiki)}/w/api.php?action=query&format=json&prop=categories&pageids={pageId}&cllimit=max&clshow=!hidden";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var categories = new List<string>();
        if (doc.RootElement.TryGetProperty("query", out var query)
            && query.TryGetProperty("pages", out var pages))
        {
            foreach (var page in pages.EnumerateObject())
            {
                if (!page.Value.TryGetProperty("categories", out var categoryElements))
                {
                    continue;
                }

                categories.AddRange(categoryElements.EnumerateArray()
                    .Select(category => category.TryGetProperty("title", out var categoryTitle)
                        ? categoryTitle.GetString() ?? ""
                        : "")
                    .Where(category => category.Length > 0));
            }
        }

        return new WikiPageMetadata(wiki, pageId, title, categories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string ApiBaseUrl(string wiki)
    {
        var language = wiki.EndsWith("wiki", StringComparison.OrdinalIgnoreCase)
            ? wiki[..^4]
            : wiki;
        return string.Create(CultureInfo.InvariantCulture, $"https://{language}.wikipedia.org");
    }

    private sealed record CacheEntry(WikiPageMetadata Metadata, DateTimeOffset ExpiresAt);
}
