using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Orders.Dal.Repositories;

public abstract class RepositoryBase
{
    protected const string Actor = "api";   // значення для CreatedBy/UpdatedBy/ChangedBy до лаби 9 (Identity)

    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    // Поточна транзакція Unit of Work (або null, якщо її не розпочато)
    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }

    // Виклик збереженої процедури через Dapper (використовують Order- і PaymentRepository)
    protected Task CallProcedureAsync(string procedure, object args, CancellationToken ct) =>
        RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(new CommandDefinition(
                procedure, args, Transaction,
                commandType: CommandType.StoredProcedure, cancellationToken: ct));
        });

    // SqlException → доменний виняток. Усі публічні методи репозиторіїв проходять через ці обгортки.
    protected static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domainEx))
        {
            throw domainEx;
        }
    }

    protected static async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (SqlException ex) when (SqlErrorTranslator.TryTranslate(ex, out var domainEx))
        {
            throw domainEx;
        }
    }
}