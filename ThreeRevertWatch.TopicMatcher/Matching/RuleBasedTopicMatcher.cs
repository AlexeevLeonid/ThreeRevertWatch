using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.TopicMatcher.Configuration;
using ThreeRevertWatch.TopicMatcher.Metadata;
using ThreeRevertWatch.TopicMatcher.Persistence;

namespace ThreeRevertWatch.TopicMatcher.Matching;

public sealed class RuleBasedTopicMatcher : ITopicMatcher
{
    private readonly ConflictTopicsOptions _options;
    private readonly ITopicArticleRepository _repository;
    private readonly IWikiPageMetadataClient _metadataClient;
    private readonly ILogger<RuleBasedTopicMatcher> _logger;

    public RuleBasedTopicMatcher(
        IOptions<ConflictTopicsOptions> options,
        ITopicArticleRepository repository,
        IWikiPageMetadataClient metadataClient,
        ILogger<RuleBasedTopicMatcher> logger)
    {
        _options = options.Value;
        _repository = repository;
        _metadataClient = metadataClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TopicMatch>> MatchAsync(RawEditEvent edit, CancellationToken cancellationToken)
    {
        var matches = new List<TopicMatch>();
        Task<WikiPageMetadata>? metadataTask = null;

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

            if (ShouldUseMetadata(topic, score))
            {
                metadataTask ??= _metadataClient.GetAsync(edit.Wiki, edit.PageId, edit.Title, cancellationToken);
                var metadata = await GetMetadataAsync(edit, metadataTask);
                if (ContainsAnyCategory(metadata.Categories, topic.ExcludeCategories)
                    || ContainsAnyCategory(metadata.Categories, topic.ExcludeCategoryKeywords))
                {
                    _logger.LogInformation(
                        "Metric={Metric} TopicId={TopicId} Wiki={Wiki} PageId={PageId} RevisionId={RevisionId} Reason={Reason}",
                        "topic_matcher_metadata_rejected",
                        topic.Id,
                        edit.Wiki,
                        edit.PageId,
                        edit.RevisionId,
                        "excluded category");
                    continue;
                }

                var exactCategoryHits = topic.IncludeCategories
                    .SelectMany(includeCategory => metadata.Categories
                        .Where(category => CategoryEquals(category, includeCategory))
                        .Select(category => (Rule: includeCategory, Category: category)))
                    .DistinctBy(hit => hit.Category, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (exactCategoryHits.Count > 0)
                {
                    score = Math.Max(score, 0.9);
                    reasons.AddRange(exactCategoryHits.Select(hit => $"category: {DisplayCategory(hit.Category)}"));
                }

                var categoryKeywordHits = topic.CategoryKeywords
                    .SelectMany(keyword => metadata.Categories
                        .Where(category => ContainsCategory(category, keyword))
                        .Select(category => (Keyword: keyword, Category: category)))
                    .DistinctBy(hit => $"{hit.Keyword}:{hit.Category}", StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (categoryKeywordHits.Count > 0)
                {
                    var categoryScore = Math.Min(0.55 + categoryKeywordHits.Count * 0.1, 0.8);
                    score = Math.Max(score, categoryScore);
                    reasons.AddRange(categoryKeywordHits
                        .Select(hit => $"category keyword: {hit.Keyword} ({DisplayCategory(hit.Category)})"));
                }
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

    private async Task<WikiPageMetadata> GetMetadataAsync(
        RawEditEvent edit,
        Task<WikiPageMetadata> metadataTask)
    {
        try
        {
            return await metadataTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Page metadata lookup failed. Wiki={Wiki} PageId={PageId} RevisionId={RevisionId}",
                edit.Wiki,
                edit.PageId,
                edit.RevisionId);
            return new WikiPageMetadata(edit.Wiki, edit.PageId, edit.Title, []);
        }
    }

    private static bool ShouldUseMetadata(ConflictTopicConfig topic, double currentScore)
        => currentScore < 0.95
           && (topic.CategoryKeywords.Count > 0
               || topic.IncludeCategories.Count > 0
               || topic.ExcludeCategories.Count > 0
               || topic.ExcludeCategoryKeywords.Count > 0);

    private static bool WikiMatches(string configuredWiki, string editWiki)
        => string.Equals(configuredWiki, editWiki, StringComparison.OrdinalIgnoreCase);

    private static bool TitleEquals(string left, string right)
        => string.Equals(NormalizeTitle(left), NormalizeTitle(right), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string title, IEnumerable<string> keywords)
        => keywords.Any(keyword => Contains(title, keyword));

    private static bool Contains(string title, string keyword)
        => !string.IsNullOrWhiteSpace(keyword)
           && NormalizeTitle(title).Contains(NormalizeTitle(keyword), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAnyCategory(IReadOnlyList<string> categories, IEnumerable<string> rules)
        => rules.Any(rule => categories.Any(category => ContainsCategory(category, rule)));

    private static bool ContainsCategory(string category, string keyword)
        => !string.IsNullOrWhiteSpace(keyword)
           && NormalizeCategory(category).Contains(NormalizeCategory(keyword), StringComparison.OrdinalIgnoreCase);

    private static bool CategoryEquals(string category, string expected)
        => string.Equals(NormalizeCategory(category), NormalizeCategory(expected), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeTitle(string title)
        => title.Replace('_', ' ').Trim();

    private static string NormalizeCategory(string category)
        => NormalizeTitle(category)
            .Replace("Категория:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Category:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

    private static string DisplayCategory(string category)
        => NormalizeCategory(category);
}
