using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public record UpdateReviewCommand(
	string Id,
	int Rating,
	string? Text,
	IReadOnlyList<string>? Pros,
	IReadOnlyList<string>? Cons,
	int? ExpectedVersion = null) : ICommand<ReviewDto>;