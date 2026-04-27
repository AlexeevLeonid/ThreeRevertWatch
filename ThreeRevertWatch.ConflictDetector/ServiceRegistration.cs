using ThreeRevertWatch.ConflictDetector.Classification;
using ThreeRevertWatch.ConflictDetector.Configuration;
using ThreeRevertWatch.ConflictDetector.Persistence;
using ThreeRevertWatch.ConflictDetector.Scoring;
using ThreeRevertWatch.ConflictDetector.State;
using ThreeRevertWatch.ConflictDetector.Wikipedia;
using ThreeRevertWatch.ConflictDetector.Workers;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.Infrastructure.Persistence;

namespace ThreeRevertWatch.ConflictDetector;

public static class ServiceRegistration
{
    public static IServiceCollection AddConflictDetector(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddThreeRevertWatchOptions(configuration);
        services.AddThreeRevertWatchKafka();
        services.AddThreeRevertWatchPersistence();
        services.AddOptions<ConflictDetectorOptions>()
            .Bind(configuration.GetSection(ConflictDetectorOptions.SectionName));
        services.AddHttpClient<IWikiRevisionClient, WikipediaRevisionClient>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ThreeRevertWatch/1.0 (local LAN monitoring; contact: admin@example.invalid)"));
        services.AddSingleton<IArticleStateStore, InMemoryArticleStateStore>();
        services.AddSingleton<IEditClassifier, ExplainableEditClassifier>();
        services.AddSingleton<ArticleConflictScoreCalculator>();
        services.AddSingleton<IParticipantClusterInferer, ParticipantClusterInferer>();
        services.AddSingleton<IArticleConflictScorer, ArticleConflictScorer>();
        services.AddSingleton<IArticleConflictRepository, PostgresArticleConflictRepository>();
        services.AddHostedService<ConflictDetectorWorker>();
        return services;
    }
}
