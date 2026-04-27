using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.Classification;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.Tests;

public sealed class EditClassifierTests
{
    [Fact]
    public async Task Revisions_A_B_A_create_exact_revert_and_edge()
    {
        var state = State();
        state.RecentRevisions.Add(Revision(1, "Alice", "sha-a"));
        state.RecentRevisions.Add(Revision(2, "Bob", "sha-b"));
        var current = Revision(3, "Carol", "sha-a");

        var edit = await new ExplainableEditClassifier().ClassifyAsync(Matched(current), current, state, CancellationToken.None);
        var edges = state.Apply(current, edit, 50, 100);

        Assert.Equal(EditActionType.ExactRevert, edit.Action);
        Assert.Contains("Bob", edit.RevertedUsers);
        Assert.Contains(2, edit.RevertedRevisionIds);
        var edge = Assert.Single(edges);
        Assert.Equal("Carol", edge.FromUser);
        Assert.Equal("Bob", edge.ToUser);
    }

    [Fact]
    public async Task Own_revision_restoration_is_self_revert()
    {
        var state = State();
        state.RecentRevisions.Add(Revision(1, "Alice", "sha-a"));
        state.RecentRevisions.Add(Revision(2, "Alice", "sha-b"));
        var current = Revision(3, "Alice", "sha-a");

        var edit = await new ExplainableEditClassifier().ClassifyAsync(Matched(current), current, state, CancellationToken.None);

        Assert.Equal(EditActionType.SelfRevert, edit.Action);
        Assert.Empty(edit.RevertedUsers);
    }

    private static ArticleRuntimeState State()
        => new("topic", "ruwiki", 10, "Article", .95);

    private static RevisionDetails Revision(long id, string user, string sha1)
        => new("ruwiki", 10, "Article", id, id - 1, user, DateTimeOffset.UtcNow.AddMinutes(id), "", [], 100, sha1);

    private static TopicMatchedEditEvent Matched(RevisionDetails revision)
        => new(
            Guid.NewGuid().ToString("n"),
            "topic",
            .95,
            ["test"],
            new RawEditEvent(
                Guid.NewGuid().ToString("n"),
                revision.RevisionId,
                revision.PageId,
                revision.Title,
                revision.Wiki,
                revision.RevisionId,
                revision.ParentRevisionId,
                revision.User,
                revision.Comment,
                revision.Tags,
                false,
                false,
                false,
                0,
                0,
                revision.Timestamp,
                DateTimeOffset.UtcNow),
            DateTimeOffset.UtcNow);
}

