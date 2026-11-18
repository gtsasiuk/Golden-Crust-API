using Orders.Contracts;

namespace Orders.Api.GrpcServices;

internal static class MoneyMapping
{
    private const decimal NanosPerUnit = 1_000_000_000m;

    public static Money ToMoney(decimal value)
    {
        var units = decimal.Truncate(value);
        return new Money { Units = (long)units, Nanos = (int)((value - units) * NanosPerUnit) };
    }
}