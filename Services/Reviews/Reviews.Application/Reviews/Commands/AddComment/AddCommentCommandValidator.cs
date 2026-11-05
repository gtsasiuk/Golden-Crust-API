using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.AddComment;

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Comment.MaxTextLength);
    }
}