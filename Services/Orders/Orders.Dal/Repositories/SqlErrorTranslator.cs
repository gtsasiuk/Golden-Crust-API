using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Orders.Domain;

namespace Orders.Dal.Repositories;

internal static class SqlErrorTranslator
{
    public static bool TryTranslate(SqlException ex, [NotNullWhen(true)] out Exception? domain)
    {
        domain = ex.Number switch
        {
            // THROW з процедур (db/orders/procedures.sql): текст наш, його можна віддавати клієнту
            50001 or 50013 => new NotFoundException(ex.Message, ex),
            >= 50002 and <= 50007 => new BusinessConflictException(ex.Message, ex),
            50010 or 50011 or 50012 or 50014 or 50020 => new ValidationException(ex.Message, ex),

            // Помилки самого SQL Server: сирий текст клієнту НЕ віддаємо
            2601 or 2627 => new BusinessConflictException("Record with the same key already exists", ex),
            547 => new BusinessConflictException("Operation violates a data constraint", ex),
            _ => null
        };
        return domain is not null;
    }
}