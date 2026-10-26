using AutoMapper;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.AddComment;

public class AddCommentCommandHandler(IReviewRepository reviews, IMapper mapper)
    : IRequestHandler<AddCommentCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.ReviewId), ct)
                     ?? throw new NotFoundException(nameof(Review), cmd.ReviewId);

        review.AddComment(new Author(cmd.CustomerId, cmd.AuthorName), cmd.Text);
        await reviews.UpdateAsync(review, ct);

        return mapper.Map<ReviewDto>(review);
    }
}