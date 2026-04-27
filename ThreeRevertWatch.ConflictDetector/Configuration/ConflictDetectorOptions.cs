namespace ThreeRevertWatch.ConflictDetector.Configuration;

public sealed class ConflictDetectorOptions
{
    public const string SectionName = "ConflictDetector";

    public int RecentRevisionWindowSize { get; set; } = 50;
    public int RecentEditWindowSize { get; set; } = 100;
    public int StateSnapshotIntervalSeconds { get; set; } = 30;
    public int RevertWindowHours { get; set; } = 6;
    public int ThreeRevertWindowHours { get; set; } = 24;
    public bool DeepDiffEnabled { get; set; }
}

