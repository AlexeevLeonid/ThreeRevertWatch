using Npgsql;

namespace ThreeRevertWatch.Infrastructure.Persistence;

public interface IPostgresConnectionFactory
{
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

