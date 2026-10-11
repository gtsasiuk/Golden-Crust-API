using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Dal.Repositories;

public sealed class CustomerRepository : RepositoryBase, ICustomerRepository
{
    private const string Columns = "Id, Email, FullName, Phone, CreatedAt, UpdatedAt";

    public CustomerRepository(SqlConnection c, Func<SqlTransaction?> tx) : base(c, tx) { }

    public Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default) =>
        RunAsync<Customer?>(async () =>
        {
            const string sql = $"SELECT {Columns} FROM dbo.Customers WHERE Id = @Id AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            await using var cmd = new SqlCommand(sql, Connection, Transaction);
            cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;           // лише параметр
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? Map(reader) : null;
        });

    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<Customer>>(async () =>
        {
            const string sql = $"SELECT {Columns} FROM dbo.Customers WHERE IsDeleted = 0 ORDER BY Id";
            await EnsureOpenAsync(ct);
            await using var cmd = new SqlCommand(sql, Connection, Transaction);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<Customer>();
            while (await reader.ReadAsync(ct)) list.Add(Map(reader));
            return list;
        });

    public Task<long> AddAsync(Customer customer, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            const string sql = @"INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
                                 OUTPUT INSERTED.Id
                                 VALUES (@Email, @FullName, @Phone, @Actor)";
            await EnsureOpenAsync(ct);
            await using var cmd = new SqlCommand(sql, Connection, Transaction);
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = customer.Email;
            cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = customer.FullName;
            cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)customer.Phone ?? DBNull.Value;
            cmd.Parameters.Add("@Actor", SqlDbType.NVarChar, 100).Value = Actor;
            return (long)(await cmd.ExecuteScalarAsync(ct))!;
        });

    public Task<bool> UpdateAsync(Customer customer, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            const string sql = @"UPDATE dbo.Customers
                                 SET Email = @Email, FullName = @FullName, Phone = @Phone,
                                     UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
                                 WHERE Id = @Id AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            await using var cmd = new SqlCommand(sql, Connection, Transaction);
            cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = customer.Id;
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = customer.Email;
            cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = customer.FullName;
            cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)customer.Phone ?? DBNull.Value;
            cmd.Parameters.Add("@Actor", SqlDbType.NVarChar, 100).Value = Actor;
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        });

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            const string sql = @"UPDATE dbo.Customers
                                 SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
                                 WHERE Id = @Id AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            await using var cmd = new SqlCommand(sql, Connection, Transaction);
            cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
            cmd.Parameters.Add("@Actor", SqlDbType.NVarChar, 100).Value = Actor;
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        });

    private static Customer Map(SqlDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Email = r.GetString(1),
        FullName = r.GetString(2),
        Phone = r.IsDBNull(3) ? null : r.GetString(3),
        CreatedAt = r.GetDateTime(4),
        UpdatedAt = r.IsDBNull(5) ? null : r.GetDateTime(5)
    };
}