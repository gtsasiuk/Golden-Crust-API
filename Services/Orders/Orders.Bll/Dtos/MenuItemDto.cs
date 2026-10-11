namespace Orders.Bll.Dtos;

public record MenuItemDto(long Id, string Name, decimal Price, bool IsAvailable);