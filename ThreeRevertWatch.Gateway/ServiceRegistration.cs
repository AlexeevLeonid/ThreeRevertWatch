using Microsoft.Extensions.Options;
using ThreeRevertWatch.Gateway.Services;
using ThreeRevertWatch.Gateway.Workers;
using ThreeRevertWatch.Infrastructure.Configuration;
using ThreeRevertWatch.Infrastructure.Kafka;

namespace ThreeRevertWatch.Gateway;

public static class ServiceRegistration
{
    public static IServiceCollection AddGatewayServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddThreeRevertWatchOptions(configuration);
        services.AddThreeRevertWatchKafka();
        services.AddSignalR();
        services.AddHttpClient<AggregatorProxyClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;
            client.BaseAddress = new Uri(options.AggregatorBaseUrl.TrimEnd('/'));
        });
        services.AddHostedService<TopicConflictBroadcastWorker>();
        services.AddHostedService<ArticleConflictBroadcastWorker>();
        return services;
    }
}

