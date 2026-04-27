using System.ComponentModel.DataAnnotations;

namespace ThreeRevertWatch.Infrastructure.Configuration;

public sealed class TopicsOptions
{
    public const string SectionName = "Topics";

    [Required]
    public string RawEdits { get; set; } = "wiki.raw-edits";

    [Required]
    public string TopicMatchedEdits { get; set; } = "wiki.topic-matched-edits";

    [Required]
    public string ArticleConflictUpdates { get; set; } = "wiki.article-conflict-updates";

    [Required]
    public string TopicConflictUpdates { get; set; } = "wiki.topic-conflict-updates";
}

