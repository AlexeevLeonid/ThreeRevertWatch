using Microsoft.Extensions.Logging;
using Npgsql;

namespace ThreeRevertWatch.Infrastructure.Persistence;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IPostgresConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IPostgresConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(DatabaseSchema.CreateSql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Conflict monitoring schema is ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed");
            throw;
        }
    }
}

