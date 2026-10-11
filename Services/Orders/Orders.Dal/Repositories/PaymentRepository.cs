using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Dal.Repositories;

public sealed class PaymentRepository : BaseDapperRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(SqlConnection c, Func<SqlTransaction?> tx) : base(c, tx, "dbo.Payments") { }

    protected override string SelectColumns => "Id, OrderId, Amount, Status, PaidAt, TransactionRef";

    protected override string InsertSql => @"
INSERT INTO dbo.Payments (OrderId, Amount, Status, CreatedBy)
OUTPUT INSERTED.Id
VALUES (@OrderId, @Amount, @Status, N'api')";

    public Task<Payment?> GetActiveByOrderIdAsync(long orderId, CancellationToken ct = default) =>
        RunAsync<Payment?>(async () =>
        {
            const string sql = @"SELECT Id, OrderId, Amount, Status, PaidAt, TransactionRef
                                 FROM dbo.Payments
                                 WHERE OrderId = @OrderId AND IsDeleted = 0
                                   AND Status <> 'Cancelled' AND Status <> 'Refunded'";
            await EnsureOpenAsync(ct);
            return await Connection.QuerySingleOrDefaultAsync<Payment>(
                new CommandDefinition(sql, new { OrderId = orderId }, Transaction, cancellationToken: ct));
        });

    public Task PayAsync(long orderId, string? transactionRef = null, CancellationToken ct = default) =>
        CallProcedureAsync("dbo.usp_PayOrder",
            new { OrderId = orderId, TransactionRef = transactionRef, ChangedBy = Actor }, ct);
}