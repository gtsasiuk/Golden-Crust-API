using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults;
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
[Authorize]
public class ReviewsController : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetList(
        [FromQuery] long? menuItemId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetReviewsListQuery(menuItemId, page, pageSize), ct));

    [HttpGet("{id}")]
    [AllowAnonymous]
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
    [RequirePermission(Permissions.ReviewsWrite)]
    [ProducesResponseType(typeof(CreatedReviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedReviewResponse>> Create(
        CreateReviewRequest request, CancellationToken ct)
    {
        // Імʼя автора береться з токена: підробити його в тілі запиту не можна
        var command = Mapper.Map<CreateReviewCommand>(request) with { AuthorName = User.GetUserName()! };

        var id = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new CreatedReviewResponse(id));
    }

    [HttpPut("{id}")]
    [RequirePermission(Permissions.ReviewsWrite)]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
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

        if (!await IsAuthorOrAdminAsync(id, ct))
            return ForbiddenProblem();

        var command = new UpdateReviewCommand(
            id, request.Rating, request.Text, request.Pros, request.Cons, expectedVersion);

        var updated = await Mediator.Send(command, ct);
        Response.Headers.ETag = $"\"{updated.Version}\"";
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [RequirePermission(Permissions.ReviewsWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!await IsAuthorOrAdminAsync(id, ct))
            return ForbiddenProblem();

        await Mediator.Send(new DeleteReviewCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id}/comments")]
    [RequirePermission(Permissions.ReviewsWrite)]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> AddComment(
        string id, AddCommentRequest request, CancellationToken ct)
    {
        var command = new AddCommentCommand(id, request.CustomerId, User.GetUserName()!, request.Text);
        var updated = await Mediator.Send(command, ct);

        Response.Headers.ETag = $"\"{updated.Version}\"";
        return CreatedAtAction(nameof(GetById), new { id }, updated);
    }

    // Змінювати й видаляти відгук може його автор або Admin.
    // Неіснуючий id дасть 404 від самого запиту.
    private async Task<bool> IsAuthorOrAdminAsync(string reviewId, CancellationToken ct)
    {
        if (User.IsAdmin()) return true;

        var review = await Mediator.Send(new GetReviewByIdQuery(reviewId), ct);
        return string.Equals(review.AuthorName, User.GetUserName(), StringComparison.Ordinal);
    }

    private ObjectResult ForbiddenProblem()
        => Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "Access denied",
            detail: "Only the author of the review or an administrator can do this");

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