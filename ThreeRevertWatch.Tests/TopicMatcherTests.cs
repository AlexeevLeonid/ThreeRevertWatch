using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.TopicMatcher.Configuration;
using ThreeRevertWatch.TopicMatcher.Matching;
using ThreeRevertWatch.TopicMatcher.Metadata;
using ThreeRevertWatch.TopicMatcher.Persistence;

namespace ThreeRevertWatch.Tests;

public sealed class TopicMatcherTests
{
    [Fact]
    public async Task Exact_seed_title_matches_topic()
    {
        var matcher = CreateMatcher();

        var matches = await matcher.MatchAsync(Edit(title: "Российско-украинская война"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal("russo_ukrainian_war", match.TopicId);
        Assert.Equal(0.95, match.Confidence);
        Assert.Equal(TopicMembershipStatus.Confirmed, match.Status);
    }

    [Fact]
    public async Task Exact_seed_page_id_matches_topic()
    {
        var matcher = CreateMatcher(seedPageIds: [42]);

        var matches = await matcher.MatchAsync(Edit(pageId: 42, title: "Any title"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal(1.0, match.Confidence);
        Assert.Contains("exact seed page id", match.Reasons);
    }

    [Fact]
    public async Task Excluded_keyword_rejects_article()
    {
        var matcher = CreateMatcher();

        var matches = await matcher.MatchAsync(Edit(title: "Украина фильм"), CancellationToken.None);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Weak_keyword_creates_candidate_match()
    {
        var matcher = CreateMatcher();

        var matches = await matcher.MatchAsync(Edit(title: "Украина и дипломатия"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal(TopicMembershipStatus.Candidate, match.Status);
        Assert.InRange(match.Confidence, 0.3, 0.7);
    }

    [Fact]
    public async Task Category_keyword_can_confirm_topic_when_title_is_weak()
    {
        var metadata = new FakeWikiPageMetadataClient(
            new WikiPageMetadata(
                "ruwiki",
                100,
                "Массированная атака",
                ["Категория:Российско-украинская война", "Категория:2026 год на Украине"]));
        var matcher = CreateMatcher(metadataClient: metadata);

        var matches = await matcher.MatchAsync(Edit(pageId: 100, title: "Массированная атака"), CancellationToken.None);

        var match = Assert.Single(matches);
        Assert.Equal(TopicMembershipStatus.Confirmed, match.Status);
        Assert.InRange(match.Confidence, 0.7, 0.9);
        Assert.Contains(match.Reasons, reason => reason.Contains("category keyword", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Excluded_category_rejects_article()
    {
        var metadata = new FakeWikiPageMetadataClient(
            new WikiPageMetadata(
                "ruwiki",
                101,
                "Россия в кино",
                ["Категория:Фильмы о России", "Категория:Российские фильмы"]));
        var matcher = CreateMatcher(metadataClient: metadata);

        var matches = await matcher.MatchAsync(Edit(pageId: 101, title: "Россия в кино"), CancellationToken.None);

        Assert.Empty(matches);
    }

    private static RuleBasedTopicMatcher CreateMatcher(
        IReadOnlyList<long>? seedPageIds = null,
        IWikiPageMetadataClient? metadataClient = null)
    {
        var options = Options.Create(new ConflictTopicsOptions
        {
            CandidateThreshold = 0.25,
            ConfirmedThreshold = 0.70,
            Topics =
            [
                new ConflictTopicConfig
                {
                    Id = "russo_ukrainian_war",
                    DisplayName = "Российско-украинская война",
                    Wiki = "ruwiki",
                    SeedPageIds = seedPageIds?.ToList() ?? [],
                    SeedTitles = ["Российско-украинская война"],
                    TitleKeywords = ["Украина", "Россия"],
                    CategoryKeywords = ["российско-украинская война", "украин"],
                    IncludeCategories = ["Российско-украинская война"],
                    ExcludeTitleKeywords = ["фильм", "песня"],
                    ExcludeCategoryKeywords = ["фильмы", "песни"]
                }
            ]
        });

        return new RuleBasedTopicMatcher(
            options,
            new InMemoryTopicArticleRepository(),
            metadataClient ?? new NoopWikiPageMetadataClient(),
            NullLogger<RuleBasedTopicMatcher>.Instance);
    }

    private static RawEditEvent Edit(long pageId = 7, string title = "Украина")
        => new(
            Guid.NewGuid().ToString("n"),
            Random.Shared.NextInt64(1, 999_999),
            pageId,
            title,
            "ruwiki",
            100,
            99,
            "Editor",
            "",
            [],
            false,
            false,
            false,
            10,
            20,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class FakeWikiPageMetadataClient : IWikiPageMetadataClient
    {
        private readonly WikiPageMetadata _metadata;

        public FakeWikiPageMetadataClient(WikiPageMetadata metadata)
        {
            _metadata = metadata;
        }

        public Task<WikiPageMetadata> GetAsync(
            string wiki,
            long pageId,
            string title,
            CancellationToken cancellationToken)
            => Task.FromResult(_metadata);
    }
}
