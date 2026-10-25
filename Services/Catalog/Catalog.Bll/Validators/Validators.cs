using Catalog.Bll.Dto;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validators;

internal static class PriceRules
{
    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule)
        => rule.GreaterThan(0)
               .LessThanOrEqualTo(MenuItem.MaxPrice)
               .PrecisionScale(10, 2, false).WithMessage("Price must have at most 2 decimal places");
}

public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator() => RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
}

public class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
{
    public UpdateCategoryDtoValidator() => RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
}

public class CreateMenuItemDtoValidator : AbstractValidator<CreateMenuItemDto>
{
    public CreateMenuItemDtoValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Price).ValidPrice();
    }
}

public class UpdateMenuItemDtoValidator : AbstractValidator<UpdateMenuItemDto>
{
    public UpdateMenuItemDtoValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Price).ValidPrice();
    }
}

public class ChangePriceDtoValidator : AbstractValidator<ChangePriceDto>
{
    public ChangePriceDtoValidator() => RuleFor(x => x.Price).ValidPrice();
}

public class UpdateDetailsDtoValidator : AbstractValidator<UpdateDetailsDto>
{
    public UpdateDetailsDtoValidator()
    {
        RuleFor(x => x.WeightGrams).GreaterThan(0).When(x => x.WeightGrams.HasValue);
        RuleFor(x => x.Calories).GreaterThanOrEqualTo(0).When(x => x.Calories.HasValue);
    }
}

public class AddImageDtoValidator : AbstractValidator<AddImageDto>
{
    public AddImageDtoValidator()
    {
        RuleFor(x => x.Url)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri)
                       && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Url must be an absolute http(s) address");
    }
}

public class MenuItemsQueryValidator : AbstractValidator<MenuItemsQuery>
{
    private static readonly string[] SortFields = ["name", "price"];
    private static readonly string[] SortDirs = ["asc", "desc"];

    public MenuItemsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue);
        RuleFor(x => x).Must(x => x.MinPrice is null || x.MaxPrice is null || x.MinPrice <= x.MaxPrice)
            .WithName("MinPrice").WithMessage("MinPrice must not exceed MaxPrice");
        RuleFor(x => x.SortBy).Must(s => SortFields.Contains(s.ToLowerInvariant()))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}");
        RuleFor(x => x.SortDir).Must(s => SortDirs.Contains(s.ToLowerInvariant()))
            .WithMessage("SortDir must be 'asc' or 'desc'");
    }
}