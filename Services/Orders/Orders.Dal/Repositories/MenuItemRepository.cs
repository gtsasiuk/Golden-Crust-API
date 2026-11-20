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

    public Task<bool> ApplyPriceChangedAsync(long id, string name, decimal price, DateTime occurredAt, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            const string sql = """
            UPDATE dbo.MenuItems
            SET Name = @Name,
                Price = @Price,
                SourceUpdatedAt = @OccurredAt,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
              AND IsDeleted = 0
              AND (
                  SourceUpdatedAt IS NULL
                  OR SourceUpdatedAt < @OccurredAt
              );
            """;

            await EnsureOpenAsync(ct);

            var affected = await Connection.ExecuteAsync(
                new CommandDefinition(sql,
                    new
                    {
                        Id = id,
                        Name = name,
                        Price = price,
                        OccurredAt = occurredAt
                    },
                    Transaction, cancellationToken: ct));

            return affected > 0;
        });

    public Task<bool> ApplyRenamedAsync(long id, string name, DateTime occurredAt, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            const string sql = """
            UPDATE dbo.MenuItems
            SET Name = @Name,
                SourceUpdatedAt = @OccurredAt,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id
              AND IsDeleted = 0
              AND (
                  SourceUpdatedAt IS NULL
                  OR SourceUpdatedAt < @OccurredAt
              );
            """;

            await EnsureOpenAsync(ct);

            var affected = await Connection.ExecuteAsync(
                new CommandDefinition(sql,
                    new
                    {
                        Id = id,
                        Name = name,
                        OccurredAt = occurredAt
                    },
                    Transaction, cancellationToken: ct));

            return affected > 0;
        });

}