namespace Catalog.Domain.Common;

public abstract class AuditableEntity
{
	public long Id { get; protected set; }

	public DateTime CreatedAt { get; private set; }
	public string? CreatedBy { get; private set; }
	public DateTime? UpdatedAt { get; private set; }
	public string? UpdatedBy { get; private set; }
	public bool IsDeleted { get; private set; }
	public byte[] RowVersion { get; private set; } = [];

	public void SoftDelete() => IsDeleted = true;

	public void StampCreated(DateTime utcNow, string? by) { CreatedAt = utcNow; CreatedBy = by; }
	public void StampUpdated(DateTime utcNow, string? by) { UpdatedAt = utcNow; UpdatedBy = by; }
}