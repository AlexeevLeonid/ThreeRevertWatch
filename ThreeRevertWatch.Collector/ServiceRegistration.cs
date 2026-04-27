using ThreeRevertWatch.Collector.Configuration;
using ThreeRevertWatch.Collector.Wikipedia;
using ThreeRevertWatch.Collector.Workers;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;

namespace ThreeRevertWatch.Collector;

public static class ServiceRegistration
{
    public static IServiceCollection AddCollector(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddThreeRevertWatchOptions(configuration);
        services.AddThreeRevertWatchKafka();
        services.AddOptions<CollectorOptions>()
            .Bind(configuration.GetSection(CollectorOptions.SectionName));
        services.AddHttpClient<IRecentChangesClient, WikipediaRecentChangesClient>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ThreeRevertWatch/1.0 (local LAN monitoring; contact: admin@example.invalid)"));
        services.AddHostedService<RecentChangesCollectorWorker>();
        return services;
    }
}
