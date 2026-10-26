using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Commands.AddComment;

public record AddCommentCommand(
    string ReviewId,
    long CustomerId,
    string AuthorName,
    string Text) : ICommand<ReviewDto>;