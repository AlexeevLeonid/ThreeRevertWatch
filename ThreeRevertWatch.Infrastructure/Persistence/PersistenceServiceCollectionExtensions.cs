using Microsoft.Extensions.DependencyInjection;

namespace ThreeRevertWatch.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddThreeRevertWatchPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IPostgresConnectionFactory, PostgresConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        return services;
    }
}

