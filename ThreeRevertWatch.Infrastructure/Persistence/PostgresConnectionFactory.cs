using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ThreeRevertWatch.Infrastructure.Persistence;

public sealed class PostgresConnectionFactory : IPostgresConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? "Host=localhost;Database=threerevertwatch;Username=threerevertwatch;Password=threerevertwatch";
    }

    public async ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

