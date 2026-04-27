using ThreeRevertWatch.Aggregator.Configuration;
using ThreeRevertWatch.Aggregator.ReadModel;
using ThreeRevertWatch.Aggregator.Services;
using ThreeRevertWatch.Aggregator.Workers;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.Infrastructure.Persistence;

namespace ThreeRevertWatch.Aggregator;

public static class ServiceRegistration
{
    public static IServiceCollection AddAggregator(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddThreeRevertWatchOptions(configuration);
        services.AddThreeRevertWatchKafka();
        services.AddThreeRevertWatchPersistence();
        services.AddOptions<ConflictTopicsCatalogOptions>()
            .Bind(configuration.GetSection(ConflictTopicsCatalogOptions.SectionName));
        services.AddSingleton<TopicScoreCalculator>();
        services.AddSingleton<IConflictReadModelStore, PostgresConflictReadModelStore>();
        services.AddSingleton<IConflictAggregationService, ConflictAggregationService>();
        services.AddHostedService<ArticleConflictUpdateWorker>();
        return services;
    }
}

