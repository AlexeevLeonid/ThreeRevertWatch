using Microsoft.Extensions.DependencyInjection;

namespace ThreeRevertWatch.Infrastructure.Kafka;

public static class KafkaServiceCollectionExtensions
{
    public static IServiceCollection AddThreeRevertWatchKafka(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IKafkaProducer<,>), typeof(KafkaProducer<,>));
        services.AddTransient(typeof(IKafkaConsumerLoop<,>), typeof(KafkaConsumerLoop<,>));
        return services;
    }
}

