using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Dal.Repositories;

namespace Orders.Dal;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);   // не відкрите: репозиторії відкривають його ліниво

        // Усі репозиторії отримують ОДНЕ з'єднання і доступ до ПОТОЧНОЇ транзакції (через Func)
        Customers = new CustomerRepository(_connection, () => _transaction);
        MenuItems = new MenuItemRepository(_connection, () => _transaction);
        Orders = new OrderRepository(_connection, () => _transaction);
        Payments = new PaymentRepository(_connection, () => _transaction);
    }

    public ICustomerRepository Customers { get; }
    public IMenuItemRepository MenuItems { get; }
    public IOrderRepository Orders { get; }
    public IPaymentRepository Payments { get; }

    // Рівень ізоляції за замовчуванням: ReadCommitted (дефолт SQL Server). Це компроміс "deadlocks vs consistency":
    // Serializable прибрав би аномалії читання, але тримав би більше блокувань і підвищив ризик deadlock.
    // Для замовлень одного закладу достатньо ReadCommitted + UPDLOCK на рядку замовлення всередині процедур.
    public async Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Transaction is already started");
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction");
        try { await _transaction.CommitAsync(ct); }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        try { await _transaction.RollbackAsync(ct); }
        catch (InvalidOperationException)
        {
            // Транзакцію вже відкотив сервер: у процедур SET XACT_ABORT ON, і після THROW вона "мертва".
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();   // неzакомічена транзакція = відкат
        await _connection.DisposeAsync();
    }
}