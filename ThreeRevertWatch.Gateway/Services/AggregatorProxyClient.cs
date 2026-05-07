using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Gateway.Services;

public sealed class AggregatorProxyClient
{
    private readonly HttpClient _httpClient;

    public AggregatorProxyClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<TopicSnapshotDto>> GetTopicsAsync(CancellationToken cancellationToken)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<TopicSnapshotDto>>("/api/conflicts/topics", cancellationToken) ?? [];

    public Task<TopicSnapshotDto?> GetTopicAsync(string topicId, CancellationToken cancellationToken)
        => GetOrNullAsync<TopicSnapshotDto>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}", cancellationToken);

    public async Task<IReadOnlyList<ArticleListItemDto>> GetArticlesAsync(string topicId, CancellationToken cancellationToken)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ArticleListItemDto>>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/articles", cancellationToken) ?? [];

    public Task<TopicActivityDto?> GetTopicActivityAsync(string topicId, int hours, CancellationToken cancellationToken)
        => GetOrNullAsync<TopicActivityDto>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/activity?hours={hours}", cancellationToken);

    public Task<ArticleConflictSnapshotDto?> GetArticleAsync(string topicId, long pageId, CancellationToken cancellationToken)
        => GetOrNullAsync<ArticleConflictSnapshotDto>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/articles/{pageId}", cancellationToken);

    public async Task<IReadOnlyList<ClassifiedEditDto>> GetEditsAsync(string topicId, long pageId, CancellationToken cancellationToken)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ClassifiedEditDto>>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/articles/{pageId}/edits", cancellationToken) ?? [];

    public async Task<object?> GetParticipantsAsync(string topicId, long pageId, CancellationToken cancellationToken)
        => await GetOrNullAsync<object>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/articles/{pageId}/participants", cancellationToken);

    private async Task<T?> GetOrNullAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
