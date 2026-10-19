using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Dal.Repositories;

public sealed class MenuItemRepository : RepositoryBase, IMenuItemRepository
{
    public MenuItemRepository(SqlConnection c, Func<SqlTransaction?> tx) : base(c, tx) { }

    public Task<MenuItem?> GetByIdAsync(long id, CancellationToken ct = default) =>
        RunAsync<MenuItem?>(async () =>
        {
            const string sql = "SELECT Id, Name, Price, IsAvailable FROM dbo.MenuItems WHERE Id = @Id AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            return await Connection.QuerySingleOrDefaultAsync<MenuItem>(
                new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
        });

    public Task<IReadOnlyList<MenuItem>> GetAllAsync(CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<MenuItem>>(async () =>
        {
            const string sql = "SELECT Id, Name, Price, IsAvailable FROM dbo.MenuItems WHERE IsDeleted = 0 ORDER BY Id";
            await EnsureOpenAsync(ct);
            var rows = await Connection.QueryAsync<MenuItem>(
                new CommandDefinition(sql, transaction: Transaction, cancellationToken: ct));
            return rows.ToList();
        });

    public Task<IReadOnlyList<MenuItem>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<MenuItem>>(async () =>
        {
            // IN @Ids: Dapper сам розгортає колекцію в параметри (@Ids1, @Ids2, ...)
            const string sql = "SELECT Id, Name, Price, IsAvailable FROM dbo.MenuItems WHERE Id IN @Ids AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            var rows = await Connection.QueryAsync<MenuItem>(
                new CommandDefinition(sql, new { Ids = ids.ToArray() }, Transaction, cancellationToken: ct));
            return rows.ToList();
        });
}