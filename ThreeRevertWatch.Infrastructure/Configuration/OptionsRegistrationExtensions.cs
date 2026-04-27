using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ThreeRevertWatch.Infrastructure.Configuration;

public static class OptionsRegistrationExtensions
{
    public static IServiceCollection AddThreeRevertWatchOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaSettings>()
            .Bind(configuration.GetSection(KafkaSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TopicsOptions>()
            .Bind(configuration.GetSection(TopicsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddOptions<ServiceUrlsOptions>()
            .Bind(configuration.GetSection(ServiceUrlsOptions.SectionName));

        return services;
    }
}

