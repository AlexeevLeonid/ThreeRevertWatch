using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.ConflictDetector.Scoring;

public sealed class ArticleConflictScoreCalculator
{
    public double Calculate(ArticleRuntimeState state)
    {
        var latest = state.RecentEdits.LastOrDefault()?.Timestamp ?? DateTimeOffset.UtcNow;
        var sixHoursAgo = latest.AddHours(-6);
        var dayAgo = latest.AddHours(-24);

        var recentEdits6h = state.RecentEdits.Count(e => e.Timestamp >= sixHoursAgo);
        var nonSelfReverts6h = state.RecentEdits.Count(e => e.Timestamp >= sixHoursAgo && IsNonSelfRevert(e));
        var reciprocalPairs24h = CountReciprocalPairs(state.RevertEdges.Where(e => e.Timestamp >= dayAgo));
        var participants24h = state.RecentEdits
            .Where(e => e.Timestamp >= dayAgo)
            .Select(e => e.User)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var cleanupRatio = CleanupRevertRatio(state.RecentEdits.Where(e => e.Timestamp >= sixHoursAgo).ToList());

        var editScore = Math.Min(recentEdits6h / 20.0, 1.0) * 10;
        var revertScore = Math.Min(nonSelfReverts6h / 6.0, 1.0) * 30;
        var reciprocalScore = Math.Min(reciprocalPairs24h / 2.0, 1.0) * 25;
        var participantScore = Math.Min(participants24h / 8.0, 1.0) * 10;
        var fragmentScore = Math.Min(state.DisputedFragments.Count / 3.0, 1.0) * 15;
        var threeRevertRiskScore = HasThreeRevertRisk(state) ? 10 : 0;
        var cleanupPenalty = Math.Min(cleanupRatio, 1.0) * 20;

        return Math.Clamp(
            editScore
            + revertScore
            + reciprocalScore
            + participantScore
            + fragmentScore
            + threeRevertRiskScore
            - cleanupPenalty,
            0,
            100);
    }

    public static ArticleConflictStatus StatusFor(double score, bool threeRevertRisk)
    {
        if (score >= 80) return ArticleConflictStatus.HighRisk;
        if (threeRevertRisk) return ArticleConflictStatus.ThreeRevertRisk;
        if (score >= 60) return ArticleConflictStatus.EditWarCandidate;
        if (score >= 40) return ArticleConflictStatus.Disputed;
        if (score >= 20) return ArticleConflictStatus.Active;
        return ArticleConflictStatus.Normal;
    }

    public static bool HasThreeRevertRisk(ArticleRuntimeState state)
    {
        var latest = state.RecentEdits.LastOrDefault()?.Timestamp ?? DateTimeOffset.UtcNow;
        var dayAgo = latest.AddHours(-24);
        return state.RecentEdits
            .Where(e => e.Timestamp >= dayAgo && IsNonSelfRevert(e) && !IsCleanup(e))
            .GroupBy(e => e.User, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() >= 3);
    }

    public static bool IsRevert(ClassifiedEditDto edit)
        => edit.Action is EditActionType.ExactRevert
            or EditActionType.PartialRevert
            or EditActionType.CounterRevert
            or EditActionType.SelfRevert
            or EditActionType.VandalismCleanup
            or EditActionType.SpamCleanup
            or EditActionType.CopyvioCleanup;

    public static bool IsNonSelfRevert(ClassifiedEditDto edit)
        => IsRevert(edit) && edit.Action != EditActionType.SelfRevert;

    public static bool IsCleanup(ClassifiedEditDto edit)
        => edit.Action is EditActionType.VandalismCleanup or EditActionType.SpamCleanup or EditActionType.CopyvioCleanup;

    private static double CleanupRevertRatio(IReadOnlyList<ClassifiedEditDto> edits)
    {
        var reverts = edits.Where(IsNonSelfRevert).ToList();
        if (reverts.Count == 0)
        {
            return 0;
        }

        return reverts.Count(IsCleanup) / (double)reverts.Count;
    }

    private static int CountReciprocalPairs(IEnumerable<RevertEdgeDto> edges)
    {
        var pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            var reverse = $"{edge.ToUser}->{edge.FromUser}";
            if (pairs.Contains(reverse))
            {
                pairs.Add(NormalizedPair(edge.FromUser, edge.ToUser));
            }
            else
            {
                pairs.Add($"{edge.FromUser}->{edge.ToUser}");
            }
        }

        return pairs.Count(pair => pair.Contains('|', StringComparison.Ordinal));
    }

    private static string NormalizedPair(string a, string b)
        => string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0 ? $"{a}|{b}" : $"{b}|{a}";
}

