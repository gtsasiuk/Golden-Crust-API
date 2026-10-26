using AutoMapper;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public class UpdateReviewCommandHandler(IReviewRepository reviews, IMapper mapper)
    : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                     ?? throw new NotFoundException(nameof(Review), cmd.Id);

        if (cmd.ExpectedVersion is { } expected && expected != review.Version)
            throw new ConflictException(
                $"Review {cmd.Id} is at version {review.Version}, but version {expected} was expected");

        review.Edit(cmd.Rating, cmd.Text, cmd.Pros, cmd.Cons);

        await reviews.UpdateAsync(review, ct);

        return mapper.Map<ReviewDto>(review);
    }
}