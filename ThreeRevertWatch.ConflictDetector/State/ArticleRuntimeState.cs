using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.ConflictDetector.State;

public sealed class ArticleRuntimeState
{
    public ArticleRuntimeState(string topicId, string wiki, long pageId, string title, double topicRelevanceScore)
    {
        TopicId = topicId;
        Wiki = wiki;
        PageId = pageId;
        Title = title;
        TopicRelevanceScore = topicRelevanceScore;
    }

    public string TopicId { get; }
    public string Wiki { get; }
    public long PageId { get; }
    public string Title { get; private set; }
    public double TopicRelevanceScore { get; private set; }
    public List<RevisionDetails> RecentRevisions { get; } = [];
    public List<ClassifiedEditDto> RecentEdits { get; } = [];
    public List<RevertEdgeDto> RevertEdges { get; } = [];
    public List<DisputedFragmentDto> DisputedFragments { get; } = [];
    public ArticleConflictSnapshotDto? CurrentSnapshot { get; set; }

    public IReadOnlyList<RevertEdgeDto> Apply(
        RevisionDetails revision,
        ClassifiedEditDto edit,
        int revisionWindowSize,
        int editWindowSize)
    {
        Title = revision.Title;
        TopicRelevanceScore = Math.Max(TopicRelevanceScore, edit.TopicId == TopicId ? TopicRelevanceScore : 0);
        RecentEdits.Add(edit);
        Trim(RecentEdits, editWindowSize);

        var newEdges = new List<RevertEdgeDto>();
        foreach (var revertedRevisionId in edit.RevertedRevisionIds)
        {
            var reverted = RecentRevisions.LastOrDefault(r => r.RevisionId == revertedRevisionId);
            if (reverted is null || string.Equals(reverted.User, edit.User, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var edge = new RevertEdgeDto(
                edit.User,
                reverted.User,
                edit.RevisionId,
                reverted.RevisionId,
                edit.Action.ToString(),
                edit.Confidence,
                edit.FragmentIds.FirstOrDefault(),
                edit.Timestamp);
            RevertEdges.Add(edge);
            newEdges.Add(edge);
        }

        RecentRevisions.Add(revision);
        Trim(RecentRevisions, revisionWindowSize);
        Trim(RevertEdges, editWindowSize);

        return newEdges;
    }

    public IReadOnlyList<RevisionDetails> FindRevisionsBetween(long revertedToRevisionId, long currentRevisionId)
    {
        var ordered = RecentRevisions.OrderBy(r => r.Timestamp).ToList();
        var fromIndex = ordered.FindIndex(r => r.RevisionId == revertedToRevisionId);
        if (fromIndex < 0)
        {
            return [];
        }

        return ordered
            .Skip(fromIndex + 1)
            .Where(r => r.RevisionId != currentRevisionId)
            .ToList();
    }

    private static void Trim<T>(List<T> values, int maxCount)
    {
        if (maxCount <= 0)
        {
            return;
        }

        while (values.Count > maxCount)
        {
            values.RemoveAt(0);
        }
    }
}

