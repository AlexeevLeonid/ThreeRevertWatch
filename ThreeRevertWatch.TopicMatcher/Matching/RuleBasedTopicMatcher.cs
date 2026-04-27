using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.TopicMatcher.Configuration;
using ThreeRevertWatch.TopicMatcher.Persistence;

namespace ThreeRevertWatch.TopicMatcher.Matching;

public sealed class RuleBasedTopicMatcher : ITopicMatcher
{
    private readonly ConflictTopicsOptions _options;
    private readonly ITopicArticleRepository _repository;

    public RuleBasedTopicMatcher(IOptions<ConflictTopicsOptions> options, ITopicArticleRepository repository)
    {
        _options = options.Value;
        _repository = repository;
    }

    public async Task<IReadOnlyList<TopicMatch>> MatchAsync(RawEditEvent edit, CancellationToken cancellationToken)
    {
        var matches = new List<TopicMatch>();

        foreach (var topic in _options.Topics.Where(t => t.IsActive && WikiMatches(t.Wiki, edit.Wiki)))
        {
            if (ContainsAny(edit.Title, topic.ExcludeTitleKeywords))
            {
                continue;
            }

            var existing = await _repository.GetAsync(topic.Id, edit.Wiki, edit.PageId, cancellationToken);
            var score = 0.0;
            var reasons = new List<string>();

            if (topic.SeedPageIds.Contains(edit.PageId))
            {
                score = Math.Max(score, 1.0);
                reasons.Add("exact seed page id");
            }

            if (topic.SeedTitles.Any(seed => TitleEquals(seed, edit.Title)))
            {
                score = Math.Max(score, 0.95);
                reasons.Add("exact seed title");
            }

            if (existing is { Status: TopicMembershipStatus.Confirmed })
            {
                score = Math.Max(score, existing.RelevanceScore);
                reasons.Add("confirmed topic_articles membership");
            }

            var includeKeywords = topic.IncludeTitleKeywords.Count > 0
                ? topic.IncludeTitleKeywords
                : topic.TitleKeywords;
            var keywordHits = includeKeywords
                .Where(keyword => Contains(edit.Title, keyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (keywordHits.Count > 0)
            {
                var keywordScore = Math.Min(0.3 + keywordHits.Count * 0.15, 0.7);
                score = Math.Max(score, keywordScore);
                reasons.AddRange(keywordHits.Select(keyword => $"title keyword: {keyword}"));
            }

            if (score >= _options.CandidateThreshold)
            {
                var status = score >= _options.ConfirmedThreshold
                    ? TopicMembershipStatus.Confirmed
                    : TopicMembershipStatus.Candidate;

                matches.Add(new TopicMatch(
                    topic.Id,
                    edit.PageId,
                    edit.Title,
                    Math.Round(score, 3),
                    reasons,
                    status));
            }
        }

        return matches;
    }

    private static bool WikiMatches(string configuredWiki, string editWiki)
        => string.Equals(configuredWiki, editWiki, StringComparison.OrdinalIgnoreCase);

    private static bool TitleEquals(string left, string right)
        => string.Equals(NormalizeTitle(left), NormalizeTitle(right), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string title, IEnumerable<string> keywords)
        => keywords.Any(keyword => Contains(title, keyword));

    private static bool Contains(string title, string keyword)
        => !string.IsNullOrWhiteSpace(keyword)
           && NormalizeTitle(title).Contains(NormalizeTitle(keyword), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeTitle(string title)
        => title.Replace('_', ' ').Trim();
}

