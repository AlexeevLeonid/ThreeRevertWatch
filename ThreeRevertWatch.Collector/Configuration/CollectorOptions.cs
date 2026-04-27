namespace ThreeRevertWatch.Collector.Configuration;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public List<string> Wikis { get; set; } = ["ruwiki"];
    public int PollIntervalSeconds { get; set; } = 5;
    public int RecentChangesLimit { get; set; } = 50;
    public List<int> AllowedNamespaces { get; set; } = [0, 1];
}

