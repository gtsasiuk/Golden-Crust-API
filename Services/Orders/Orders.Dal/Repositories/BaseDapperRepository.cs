using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;

namespace Orders.Dal.Repositories;

public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    private readonly string _table;

    protected BaseDapperRepository(SqlConnection c, Func<SqlTransaction?> tx, string table) : base(c, tx)
        => _table = table;

    // Усе нижче це КОНСТАНТИ з коду нащадків, а не вхідні дані користувача, тому підстановка в текст запиту безпечна.
    protected abstract string SelectColumns { get; }
    protected abstract string InsertSql { get; }          // має повертати новий Id (OUTPUT INSERTED.Id або SELECT)
    protected virtual string FromClause => _table;        // Order перевизначає: JOIN з OrderDetails

    public virtual Task<T?> GetByIdAsync(long id, CancellationToken ct = default) =>
        RunAsync<T?>(async () =>
        {
            await EnsureOpenAsync(ct);
            var sql = $"SELECT {SelectColumns} FROM {FromClause} WHERE Id = @Id AND IsDeleted = 0";
            return await Connection.QuerySingleOrDefaultAsync<T>(
                new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
        });

    public virtual Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<T>>(async () =>
        {
            await EnsureOpenAsync(ct);
            var sql = $"SELECT {SelectColumns} FROM {FromClause} WHERE IsDeleted = 0 ORDER BY Id";
            var rows = await Connection.QueryAsync<T>(
                new CommandDefinition(sql, transaction: Transaction, cancellationToken: ct));
            return rows.ToList();
        });

    public virtual Task<long> AddAsync(T entity, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            // Dapper бере з entity лише ті властивості, імена яких зустрічаються в SQL як @Name
            return await Connection.ExecuteScalarAsync<long>(
                new CommandDefinition(InsertSql, entity, Transaction, cancellationToken: ct));
        });

    // Soft delete: у схемі є IsDeleted (історію замовлень і платежів фізично не видаляємо)
    public virtual Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            var sql = $"UPDATE {_table} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0";
            var rows = await Connection.ExecuteAsync(
                new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
            return rows > 0;
        });
}