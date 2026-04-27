using Microsoft.AspNetCore.SignalR;

namespace ThreeRevertWatch.Gateway.Hubs;

public sealed class ConflictsHub : Hub<IConflictsHubClient>
{
    public Task JoinTopics() => Groups.AddToGroupAsync(Context.ConnectionId, "conflict-topics");

    public Task LeaveTopics() => Groups.RemoveFromGroupAsync(Context.ConnectionId, "conflict-topics");

    public Task JoinTopic(string topicId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"conflict-topic:{topicId}");

    public Task LeaveTopic(string topicId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conflict-topic:{topicId}");

    public Task JoinArticle(string topicId, long pageId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"conflict-article:{topicId}:{pageId}");

    public Task LeaveArticle(string topicId, long pageId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conflict-article:{topicId}:{pageId}");
}

