using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;

namespace Orders.Dal.Repositories;

public sealed class ProcessedEventRepository : RepositoryBase,
    IProcessedEventRepository
{
    public ProcessedEventRepository(
        SqlConnection connection,
        Func<SqlTransaction?> transaction)
        : base(connection, transaction)
    {
    }

    public async Task<bool> TryAddAsync(
        Guid eventId,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.ProcessedEvents (EventId)
            VALUES (@EventId);
            """;

        await EnsureOpenAsync(ct);

        try
        {
            await Connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new { EventId = eventId },
                    Transaction,
                    cancellationToken: ct));

            return true;
        }
        catch (SqlException ex) when (
            ex.Number is 2601 or 2627)
        {
            // Унікальний EventId уже існує.
            return false;
        }
    }
}
