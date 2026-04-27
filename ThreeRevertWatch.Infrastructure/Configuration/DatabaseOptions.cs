namespace ThreeRevertWatch.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool MigrateOnStartup { get; set; } = true;
}

