using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Dtos;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.WebApi.Models;

namespace Reviews.WebApi.Controllers;

[Route("api/reviews")]
[Produces("application/json")]
public class ReviewsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetList(
        [FromQuery] long? menuItemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetReviewsListQuery(menuItemId, page, pageSize), ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
    {
        var review = await Mediator.Send(new GetReviewByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{review.Version}\"";
        return Ok(review);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedReviewResponse>> Create(
        CreateReviewRequest request, CancellationToken ct)
    {
        var id = await Mediator.Send(Mapper.Map<CreateReviewCommand>(request), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedReviewResponse(id));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Update(
        string id,
        UpdateReviewRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        if (!TryReadVersion(ifMatch, out var expectedVersion))
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid If-Match header",
                detail: "If-Match must contain the review version from ETag, for example \"3\"");

        var command = new UpdateReviewCommand(
            id, request.Rating, request.Text, request.Pros, request.Cons, expectedVersion);

        var updated = await Mediator.Send(command, ct);
        Response.Headers.ETag = $"\"{updated.Version}\"";
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteReviewCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> AddComment(
        string id, AddCommentRequest request, CancellationToken ct)
    {
        var command = new AddCommentCommand(id, request.CustomerId, request.AuthorName, request.Text);
        var updated = await Mediator.Send(command, ct);

        Response.Headers.ETag = $"\"{updated.Version}\"";
        return CreatedAtAction(nameof(GetById), new { id }, updated);
    }

    private static bool TryReadVersion(string? ifMatch, out int? version)
    {
        version = null;
        var raw = ifMatch?.Trim().Trim('"');
        if (string.IsNullOrEmpty(raw) || raw == "*")
            return true;

        if (int.TryParse(raw, out var parsed) && parsed >= 0)
        {
            version = parsed;
            return true;
        }
        return false;
    }
}