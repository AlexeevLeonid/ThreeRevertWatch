using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;
using ThreeRevertWatch.Infrastructure.Persistence;
using ThreeRevertWatch.TopicMatcher.Configuration;
using ThreeRevertWatch.TopicMatcher.Matching;
using ThreeRevertWatch.TopicMatcher.Metadata;
using ThreeRevertWatch.TopicMatcher.Persistence;
using ThreeRevertWatch.TopicMatcher.Workers;

namespace ThreeRevertWatch.TopicMatcher;

public static class ServiceRegistration
{
    public static IServiceCollection AddTopicMatcher(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddThreeRevertWatchOptions(configuration);
        services.AddThreeRevertWatchKafka();
        services.AddThreeRevertWatchPersistence();
        services.AddOptions<ConflictTopicsOptions>()
            .Bind(configuration.GetSection(ConflictTopicsOptions.SectionName))
            .Validate(options => options.Topics.Count > 0, "At least one conflict topic is required")
            .ValidateOnStart();
        services.AddOptions<PageMetadataOptions>()
            .Bind(configuration.GetSection(PageMetadataOptions.SectionName));
        services.AddSingleton<ITopicMatcher, RuleBasedTopicMatcher>();
        services.AddHttpClient<IWikiPageMetadataClient, WikipediaPageMetadataClient>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ThreeRevertWatch/1.0 (local LAN monitoring; contact: admin@example.invalid)"));
        services.AddSingleton<ITopicArticleRepository, PostgresTopicArticleRepository>();
        services.AddSingleton<IConflictTopicSeeder, PostgresConflictTopicSeeder>();
        services.AddHostedService<TopicMatcherWorker>();
        return services;
    }
}
