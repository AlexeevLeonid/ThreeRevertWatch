using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Aggregator.Services;

public sealed class TopicScoreCalculator
{
    public double Calculate(IReadOnlyList<ArticleConflictSnapshotDto> articles)
    {
        var activeArticleCount = articles.Count(a => a.ConflictScore >= 30);
        var highRiskArticleCount = articles.Count(a => a.ConflictScore >= 70);
        var recentReverts = articles.Sum(a => a.RecentRevertCount);
        var participants = articles.Sum(a => a.RecentParticipantCount);

        var activeArticleScore = Math.Min(activeArticleCount / 10.0, 1.0) * 20;
        var revertScore = Math.Min(recentReverts / 30.0, 1.0) * 25;
        var participantScore = Math.Min(participants / 25.0, 1.0) * 15;
        var highRiskScore = Math.Min(highRiskArticleCount / 5.0, 1.0) * 25;
        var spreadScore = Math.Min(activeArticleCount / 8.0, 1.0) * 15;

        return Math.Clamp(
            activeArticleScore + revertScore + participantScore + highRiskScore + spreadScore,
            0,
            100);
    }

    public static TopicConflictStatus StatusFor(double score)
    {
        if (score >= 80) return TopicConflictStatus.HighRisk;
        if (score >= 60) return TopicConflictStatus.ActiveEditWarCandidate;
        if (score >= 40) return TopicConflictStatus.ActiveDispute;
        if (score >= 20) return TopicConflictStatus.Watching;
        return TopicConflictStatus.Quiet;
    }
}

