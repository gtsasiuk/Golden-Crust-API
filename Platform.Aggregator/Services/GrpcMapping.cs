namespace Platform.Aggregator.Services;

internal static class GrpcMapping
{
    private const decimal NanosPerUnit = 1_000_000_000m;

    public static decimal ToDecimal(this Catalog.Contracts.Money? money)
        => money is null ? 0m : money.Units + money.Nanos / NanosPerUnit;

    public static decimal ToDecimal(this Orders.Contracts.Money? money)
        => money is null ? 0m : money.Units + money.Nanos / NanosPerUnit;
}