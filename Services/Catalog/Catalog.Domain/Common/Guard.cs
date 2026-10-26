using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Common;

/// Спільні перевірки інваріантів. internal: потрібні лише всередині Domain.
internal static class Guard
{
    public static string NotBlank(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} must not be empty");

        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters");

        return value;
    }

    public static string? OptionalMax(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters");
        return value;
    }

    public static decimal Price(decimal value, decimal max)
    {
        if (value <= 0)
            throw new DomainException("Price must be positive");
        if (value > max)
            throw new DomainException($"Price must not exceed {max}");
        if (decimal.Round(value, 2) != value)
            throw new DomainException("Price must have at most 2 decimal places");   // колонка DECIMAL(10,2)
        return value;
    }
}