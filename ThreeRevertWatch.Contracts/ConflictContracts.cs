namespace ThreeRevertWatch.Contracts;

public sealed record RawEditEvent(
    string EventId,
    long WikiEditId,
    long PageId,
    string Title,
    string Wiki,
    long? RevisionId,
    long? ParentRevisionId,
    string User,
    string Comment,
    IReadOnlyList<string> Tags,
    bool IsBot,
    bool IsMinor,
    bool IsNew,
    int OldLength,
    int NewLength,
    DateTimeOffset Timestamp,
    DateTimeOffset CollectedAt);

public sealed record ConflictTopic(
    string Id,
    string DisplayName,
    string Wiki,
    bool IsActive,
    IReadOnlyList<long> SeedPageIds,
    IReadOnlyList<string> SeedTitles,
    IReadOnlyList<TopicRuleDto> Rules,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TopicRuleDto(
    string Type,
    string Value,
    int? MaxDepth,
    double Weight);

public enum TopicMembershipStatus
{
    Confirmed,
    Candidate,
    Rejected,
    Excluded
}

public sealed record TopicArticleMembershipDto(
    string TopicId,
    string Wiki,
    long PageId,
    string Title,
    TopicMembershipStatus Status,
    double RelevanceScore,
    IReadOnlyList<string> Reasons,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);

public sealed record TopicMatchedEditEvent(
    string EventId,
    string TopicId,
    double TopicRelevanceScore,
    IReadOnlyList<string> MatchReasons,
    RawEditEvent RawEdit,
    DateTimeOffset MatchedAt);

public enum EditActionType
{
    Unknown,
    OrdinaryEdit,
    MaintenanceEdit,
    BotEdit,
    ExactRevert,
    PartialRevert,
    Restoration,
    CounterRevert,
    SelfRevert,
    VandalismCleanup,
    SpamCleanup,
    CopyvioCleanup,
    TalkPageCoordination
}

public sealed record ClassifiedEditDto(
    string Wiki,
    string TopicId,
    long PageId,
    string Title,
    long RevisionId,
    long? ParentRevisionId,
    string User,
    DateTimeOffset Timestamp,
    string Comment,
    IReadOnlyList<string> Tags,
    EditActionType Action,
    double Confidence,
    IReadOnlyList<string> RevertedUsers,
    IReadOnlyList<long> RevertedRevisionIds,
    IReadOnlyList<string> FragmentIds,
    IReadOnlyList<string> Flags);

public sealed record RevertEdgeDto(
    string FromUser,
    string ToUser,
    long FromRevisionId,
    long ToRevisionId,
    string RevertType,
    double Confidence,
    string? FragmentId,
    DateTimeOffset Timestamp);

public sealed record ParticipantStatsDto(
    string User,
    int EditCount,
    int RevertCount,
    int RevertedByOthersCount,
    IReadOnlyList<string> FrequentlyRevertsUsers,
    IReadOnlyList<string> FrequentlyRevertedByUsers);

public sealed record ParticipantClusterDto(
    string ClusterId,
    IReadOnlyList<string> Users,
    double Confidence,
    IReadOnlyList<string> Evidence);

public sealed record DisputedFragmentDto(
    string FragmentId,
    string Label,
    int ChangeCount,
    int RevertCount,
    IReadOnlyList<string> Participants,
    DateTimeOffset LastChangedAt);

public enum ArticleConflictStatus
{
    Normal,
    Active,
    Disputed,
    EditWarCandidate,
    ThreeRevertRisk,
    HighRisk
}

public sealed record ArticleConflictSnapshotDto(
    string TopicId,
    string Wiki,
    long PageId,
    string Title,
    double TopicRelevanceScore,
    double ConflictScore,
    ArticleConflictStatus Status,
    int RecentEditCount,
    int RecentRevertCount,
    int RecentParticipantCount,
    IReadOnlyList<ClassifiedEditDto> RecentEdits,
    IReadOnlyList<ParticipantStatsDto> Participants,
    IReadOnlyList<ParticipantClusterDto> ParticipantClusters,
    IReadOnlyList<DisputedFragmentDto> DisputedFragments,
    IReadOnlyList<RevertEdgeDto> RevertEdges,
    IReadOnlyList<string> Evidence,
    DateTimeOffset UpdatedAt);

public enum TopicConflictStatus
{
    Quiet,
    Watching,
    ActiveDispute,
    ActiveEditWarCandidate,
    HighRisk
}

public sealed record ArticleListItemDto(
    string TopicId,
    string Wiki,
    long PageId,
    string Title,
    double RelevanceScore,
    double ConflictScore,
    int RecentEditCount,
    int RecentRevertCount,
    int RecentParticipantCount,
    ArticleConflictStatus Status,
    DateTimeOffset LastEditAt);

public sealed record TopicSnapshotDto(
    string TopicId,
    string DisplayName,
    double ConflictScore,
    TopicConflictStatus Status,
    int ActiveArticleCount,
    int RecentEditCount,
    int RecentRevertCount,
    int RecentParticipantCount,
    IReadOnlyList<ArticleListItemDto> Articles,
    IReadOnlyList<string> Evidence,
    DateTimeOffset UpdatedAt);

public sealed record ArticleConflictUpdateEvent(
    string EventId,
    string TopicId,
    long PageId,
    ClassifiedEditDto Edit,
    ArticleConflictSnapshotDto Snapshot,
    DateTimeOffset ProducedAt);

public sealed record TopicConflictUpdateEvent(
    string EventId,
    TopicSnapshotDto Snapshot,
    DateTimeOffset ProducedAt);

public sealed record RevisionDetails(
    string Wiki,
    long PageId,
    string Title,
    long RevisionId,
    long? ParentRevisionId,
    string User,
    DateTimeOffset Timestamp,
    string Comment,
    IReadOnlyList<string> Tags,
    int? Size,
    string? Sha1);

public sealed record ConflictAlertDto(
    string TopicId,
    long PageId,
    string Title,
    double ConflictScore,
    ArticleConflictStatus Status,
    IReadOnlyList<string> Evidence,
    DateTimeOffset ProducedAt);

