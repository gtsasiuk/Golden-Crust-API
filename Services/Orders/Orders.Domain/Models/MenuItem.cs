namespace Orders.Domain;

// Локальна копія довідника Catalog (власник даних: Catalog). Тут лише читаємо.
public class MenuItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}