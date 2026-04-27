using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.Scoring;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.Tests;

public sealed class ParticipantClusterInfererTests
{
    [Fact]
    public void Mutual_reverts_split_users_into_neutral_clusters()
    {
        var state = State();
        state.RevertEdges.Add(Edge("A", "B"));
        state.RevertEdges.Add(Edge("B", "A"));

        var clusters = new ParticipantClusterInferer().Infer(state);

        Assert.Contains(clusters, c => c.ClusterId == "Cluster A" && c.Users.Contains("A"));
        Assert.Contains(clusters, c => c.ClusterId == "Cluster B" && c.Users.Contains("B"));
    }

    [Fact]
    public void Users_reverting_same_target_can_form_weak_alignment_cluster()
    {
        var state = State();
        state.RevertEdges.Add(Edge("A", "Target"));
        state.RevertEdges.Add(Edge("C", "Target"));

        var clusters = new ParticipantClusterInferer().Infer(state);

        Assert.Contains(clusters, c => c.Users.Contains("A") && c.Users.Contains("C") && c.Confidence >= .5);
    }

    [Fact]
    public void Insufficient_data_returns_empty_clusters()
    {
        var clusters = new ParticipantClusterInferer().Infer(State());

        Assert.Empty(clusters);
    }

    private static ArticleRuntimeState State()
        => new("topic", "ruwiki", 10, "Article", .95);

    private static RevertEdgeDto Edge(string from, string to)
        => new(from, to, 1, 2, "ExactRevert", .9, null, DateTimeOffset.UtcNow);
}

