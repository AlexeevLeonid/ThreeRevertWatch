using Microsoft.Extensions.Options;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.TopicMatcher.Configuration;
using ThreeRevertWatch.TopicMatcher.Matching;
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

    private static RuleBasedTopicMatcher CreateMatcher(IReadOnlyList<long>? seedPageIds = null)
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
                    ExcludeTitleKeywords = ["фильм", "песня"]
                }
            ]
        });

        return new RuleBasedTopicMatcher(options, new InMemoryTopicArticleRepository());
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
}
