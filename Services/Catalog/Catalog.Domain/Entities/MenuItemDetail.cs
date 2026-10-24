using Catalog.Domain.Common;
using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class MenuItemDetail
{
    public const int DescriptionMaxLength = 2000;
    public const int IngredientsMaxLength = 1000;

    public long MenuItemId { get; private set; }
    public string? Description { get; private set; }
    public string? Ingredients { get; private set; }
    public int? WeightGrams { get; private set; }
    public int? Calories { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private MenuItemDetail() { }

    internal MenuItemDetail(string? description, string? ingredients, int? weightGrams, int? calories)
        => Apply(description, ingredients, weightGrams, calories);

    internal void Update(string? description, string? ingredients, int? weightGrams, int? calories)
    {
        Apply(description, ingredients, weightGrams, calories);
        UpdatedAt = DateTime.UtcNow;
    }

    private void Apply(string? description, string? ingredients, int? weightGrams, int? calories)
    {
        if (weightGrams is <= 0) throw new DomainException("WeightGrams must be positive");
        if (calories is < 0) throw new DomainException("Calories must not be negative");

        Description = Guard.OptionalMax(description, nameof(Description), DescriptionMaxLength);
        Ingredients = Guard.OptionalMax(ingredients, nameof(Ingredients), IngredientsMaxLength);
        WeightGrams = weightGrams;
        Calories = calories;
    }
}