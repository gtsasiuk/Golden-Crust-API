namespace Orders.Dal.Interfaces;

public interface IProcessedEventRepository
{
    Task<bool> TryAddAsync(Guid eventId, CancellationToken ct = default);
}
