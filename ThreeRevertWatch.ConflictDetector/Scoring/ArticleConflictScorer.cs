using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.ConflictDetector.Scoring;

public sealed class ArticleConflictScorer : IArticleConflictScorer
{
    private readonly ArticleConflictScoreCalculator _calculator;
    private readonly IParticipantClusterInferer _clusterInferer;

    public ArticleConflictScorer(ArticleConflictScoreCalculator calculator, IParticipantClusterInferer clusterInferer)
    {
        _calculator = calculator;
        _clusterInferer = clusterInferer;
    }

    public ArticleConflictSnapshotDto BuildSnapshot(ArticleRuntimeState state, ClassifiedEditDto latestEdit)
    {
        var score = _calculator.Calculate(state);
        var threeRevertRisk = ArticleConflictScoreCalculator.HasThreeRevertRisk(state);
        var latest = latestEdit.Timestamp;
        var sixHoursAgo = latest.AddHours(-6);
        var dayAgo = latest.AddHours(-24);
        var recentEdits = state.RecentEdits.Where(e => e.Timestamp >= sixHoursAgo).ToList();
        var recent24 = state.RecentEdits.Where(e => e.Timestamp >= dayAgo).ToList();
        var participants = BuildParticipantStats(state);
        var evidence = BuildEvidence(state, latestEdit, score, threeRevertRisk);

        return new ArticleConflictSnapshotDto(
            state.TopicId,
            state.Wiki,
            state.PageId,
            state.Title,
            state.TopicRelevanceScore,
            Math.Round(score, 2),
            ArticleConflictScoreCalculator.StatusFor(score, threeRevertRisk),
            recentEdits.Count,
            recentEdits.Count(ArticleConflictScoreCalculator.IsNonSelfRevert),
            recent24.Select(e => e.User).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            state.RecentEdits.OrderByDescending(e => e.Timestamp).Take(30).ToList(),
            participants,
            _clusterInferer.Infer(state),
            state.DisputedFragments.ToList(),
            state.RevertEdges.OrderByDescending(e => e.Timestamp).Take(50).ToList(),
            evidence,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<ParticipantStatsDto> BuildParticipantStats(ArticleRuntimeState state)
    {
        return state.RecentEdits
            .GroupBy(e => e.User, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var user = group.Key;
                var outgoing = state.RevertEdges
                    .Where(edge => string.Equals(edge.FromUser, user, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var incoming = state.RevertEdges
                    .Where(edge => string.Equals(edge.ToUser, user, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return new ParticipantStatsDto(
                    user,
                    group.Count(),
                    outgoing.Count,
                    incoming.Count,
                    outgoing.GroupBy(edge => edge.ToUser, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .Take(5)
                        .ToList(),
                    incoming.GroupBy(edge => edge.FromUser, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .Take(5)
                        .ToList());
            })
            .OrderByDescending(p => p.EditCount + p.RevertCount + p.RevertedByOthersCount)
            .ToList();
    }

    private static IReadOnlyList<string> BuildEvidence(
        ArticleRuntimeState state,
        ClassifiedEditDto latestEdit,
        double score,
        bool threeRevertRisk)
    {
        var evidence = new List<string>
        {
            $"latest action: {latestEdit.Action} ({latestEdit.Confidence:P0})",
            $"conflict score: {score:0.0}"
        };

        if (latestEdit.RevertedUsers.Count > 0)
        {
            evidence.Add($"{latestEdit.User} reverted {string.Join(", ", latestEdit.RevertedUsers)}");
        }

        var reciprocal = state.RevertEdges.Any(a => state.RevertEdges.Any(b =>
            string.Equals(a.FromUser, b.ToUser, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.ToUser, b.FromUser, StringComparison.OrdinalIgnoreCase)));
        if (reciprocal)
        {
            evidence.Add("reciprocal revert pair detected");
        }

        if (threeRevertRisk)
        {
            evidence.Add("three-revert risk: at least one participant made 3+ non-cleanup non-self reverts in 24h");
        }

        if (state.DisputedFragments.Count == 0)
        {
            evidence.Add("disputed fragments require optional deep diff and are not inferred in MVP");
        }

        return evidence;
    }
}

