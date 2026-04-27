using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.Frontend.Configuration;

namespace ThreeRevertWatch.Frontend.Services;

public sealed class ConflictRealtimeClient : IAsyncDisposable
{
    private readonly GatewayOptions _options;
    private HubConnection? _connection;

    public ConflictRealtimeClient(IOptions<GatewayOptions> options)
    {
        _options = options.Value;
    }

    public async Task ConnectAsync(
        Func<TopicSnapshotDto, Task>? onTopicUpdated = null,
        Func<ArticleConflictUpdateEvent, Task>? onArticleUpdated = null,
        Func<ConflictAlertDto, Task>? onAlert = null,
        CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_options.BaseUrl.TrimEnd('/')}/hubs/conflicts")
            .WithAutomaticReconnect()
            .Build();

        if (onTopicUpdated is not null)
        {
            _connection.On("TopicSnapshotUpdated", onTopicUpdated);
        }

        if (onArticleUpdated is not null)
        {
            _connection.On("ArticleConflictUpdated", onArticleUpdated);
        }

        if (onAlert is not null)
        {
            _connection.On("ConflictAlert", onAlert);
        }

        await _connection.StartAsync(cancellationToken);
    }

    public Task JoinTopicsAsync() => InvokeAsync("JoinTopics");

    public Task JoinTopicAsync(string topicId) => InvokeAsync("JoinTopic", topicId);

    public Task JoinArticleAsync(string topicId, long pageId) => InvokeAsync("JoinArticle", topicId, pageId);

    private Task InvokeAsync(string methodName, params object?[] args)
        => _connection is null ? Task.CompletedTask : _connection.InvokeCoreAsync(methodName, args);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}

