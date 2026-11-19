namespace Orders.Api.Metrics;

public sealed class OrdersSnapshot
{
	private IReadOnlyDictionary<string, int> _activeByStatus = new Dictionary<string, int>();

	public int Get(string status) =>
		Volatile.Read(ref _activeByStatus).TryGetValue(status, out var count) ? count : 0;

	public void Update(IReadOnlyDictionary<string, int> value) => Volatile.Write(ref _activeByStatus, value);
}