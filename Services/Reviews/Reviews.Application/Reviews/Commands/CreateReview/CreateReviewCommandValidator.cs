using FluentValidation;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.MenuItemId).GreaterThan(0);
        RuleFor(x => x.MenuItemName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Text).MaximumLength(Review.MaxTextLength);
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId.HasValue);
        RuleFor(x => x.Photos).Must(p => p!.Count <= Review.MaxPhotos)
            .When(x => x.Photos is not null)
            .WithMessage($"A review can have at most {Review.MaxPhotos} photos");

        RuleFor(x => x.CustomerId)
            .MustAsync(async (cmd, customerId, ct) =>
                !await reviews.ExistsAsync(cmd.MenuItemId, customerId, ct))
            .When(x => x.MenuItemId > 0 && x.CustomerId > 0)
            .WithMessage("You have already reviewed this menu item");
    }
}