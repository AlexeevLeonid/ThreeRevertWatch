using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.Scoring;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.Tests;

public sealed class ArticleScoringTests
{
    [Fact]
    public void Ordinary_edits_give_low_score()
    {
        var state = State();
        for (var i = 0; i < 5; i++)
        {
            state.RecentEdits.Add(Edit(i, "User", EditActionType.OrdinaryEdit));
        }

        var score = new ArticleConflictScoreCalculator().Calculate(state);

        Assert.True(score < 20);
    }

    [Fact]
    public void Reciprocal_reverts_give_high_score()
    {
        var state = State();
        for (var i = 0; i < 6; i++)
        {
            state.RecentEdits.Add(Edit(i, i % 2 == 0 ? "A" : "B", EditActionType.ExactRevert, ["A", "B"]));
        }

        state.RevertEdges.Add(new RevertEdgeDto("A", "B", 10, 9, "ExactRevert", .95, null, DateTimeOffset.UtcNow));
        state.RevertEdges.Add(new RevertEdgeDto("B", "A", 11, 10, "ExactRevert", .95, null, DateTimeOffset.UtcNow));

        var score = new ArticleConflictScoreCalculator().Calculate(state);

        Assert.True(score >= 50);
    }

    [Fact]
    public void Cleanup_reverts_reduce_score()
    {
        var conflict = State();
        var cleanup = State();
        for (var i = 0; i < 6; i++)
        {
            conflict.RecentEdits.Add(Edit(i, $"User{i}", EditActionType.ExactRevert, ["Target"]));
            cleanup.RecentEdits.Add(Edit(i, $"User{i}", EditActionType.VandalismCleanup, ["Target"]));
        }

        var calculator = new ArticleConflictScoreCalculator();

        Assert.True(calculator.Calculate(cleanup) < calculator.Calculate(conflict));
    }

    [Fact]
    public void Three_revert_risk_changes_status()
    {
        var state = State();
        for (var i = 0; i < 3; i++)
        {
            state.RecentEdits.Add(Edit(i, "A", EditActionType.ExactRevert, ["B"]));
        }

        var score = new ArticleConflictScoreCalculator().Calculate(state);
        var status = ArticleConflictScoreCalculator.StatusFor(score, ArticleConflictScoreCalculator.HasThreeRevertRisk(state));

        Assert.Equal(ArticleConflictStatus.ThreeRevertRisk, status);
    }

    private static ArticleRuntimeState State()
        => new("topic", "ruwiki", 10, "Article", .95);

    private static ClassifiedEditDto Edit(int index, string user, EditActionType action, IReadOnlyList<string>? revertedUsers = null)
        => new(
            "ruwiki",
            "topic",
            10,
            "Article",
            index + 1,
            index,
            user,
            DateTimeOffset.UtcNow.AddMinutes(index),
            "",
            [],
            action,
            .9,
            revertedUsers ?? [],
            revertedUsers is null ? [] : [index],
            [],
            []);
}

