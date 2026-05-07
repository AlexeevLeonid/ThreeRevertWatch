using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Frontend.Services;

public sealed class GatewayApiClient
{
    private readonly HttpClient _httpClient;

    public GatewayApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<TopicSnapshotDto>> GetTopicsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<TopicSnapshotDto>>("/api/conflicts/topics", cancellationToken) ?? [];

    public Task<TopicSnapshotDto?> GetTopicAsync(string topicId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TopicSnapshotDto>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}", cancellationToken);

    public async Task<IReadOnlyList<ArticleListItemDto>> GetArticlesAsync(string topicId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ArticleListItemDto>>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/articles", cancellationToken) ?? [];

    public Task<TopicActivityDto?> GetTopicActivityAsync(string topicId, int hours = 24, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TopicActivityDto>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/activity?hours={hours}", cancellationToken);

    public Task<ArticleConflictSnapshotDto?> GetArticleAsync(string topicId, long pageId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ArticleConflictSnapshotDto>($"/api/conflicts/topics/{Uri.EscapeDataString(topicId)}/articles/{pageId}", cancellationToken);

    public Task<PipelineMetricsDto?> GetPipelineMetricsAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PipelineMetricsDto>("/api/pipeline/metrics", cancellationToken);
}
