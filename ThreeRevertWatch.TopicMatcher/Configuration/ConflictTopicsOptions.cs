namespace ThreeRevertWatch.TopicMatcher.Configuration;

public sealed class ConflictTopicsOptions
{
    public const string SectionName = "ConflictTopics";

    public double CandidateThreshold { get; set; } = 0.25;
    public double ConfirmedThreshold { get; set; } = 0.70;
    public double DetectorPublishThreshold { get; set; } = 0.50;
    public List<ConflictTopicConfig> Topics { get; set; } = [];
}

public sealed class ConflictTopicConfig
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Wiki { get; set; } = "ruwiki";
    public bool IsActive { get; set; } = true;
    public List<long> SeedPageIds { get; set; } = [];
    public List<string> SeedTitles { get; set; } = [];
    public List<string> TitleKeywords { get; set; } = [];
    public List<string> CategoryKeywords { get; set; } = [];
    public List<string> IncludeTitleKeywords { get; set; } = [];
    public List<string> ExcludeTitleKeywords { get; set; } = [];
}

