namespace ThreeRevertWatch.TopicMatcher.Persistence;

public interface IConflictTopicSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

