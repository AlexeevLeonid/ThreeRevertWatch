using System.ComponentModel.DataAnnotations;

namespace ThreeRevertWatch.Infrastructure.Configuration;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    [Required]
    public string BootstrapServers { get; set; } = "localhost:29092";

    [Required]
    public string GroupId { get; set; } = "threerevertwatch";

    public string AutoOffsetReset { get; set; } = "earliest";
    public bool EnableAutoCommit { get; set; }
    public int SessionTimeoutMs { get; set; } = 30_000;
    public int MaxPollIntervalMs { get; set; } = 300_000;
    public bool MetricsEnabled { get; set; } = true;
    public int MetricsLogIntervalSeconds { get; set; } = 30;
    public int SlowMessageThresholdMs { get; set; } = 1_000;
}

