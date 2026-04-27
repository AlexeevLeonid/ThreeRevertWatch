namespace ThreeRevertWatch.Aggregator.Configuration;

public sealed class ConflictTopicsCatalogOptions
{
    public const string SectionName = "ConflictTopics";

    public List<ConflictTopicCatalogItem> Topics { get; set; } = [];
}

public sealed class ConflictTopicCatalogItem
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

