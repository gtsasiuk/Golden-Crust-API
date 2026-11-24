using Auth.Api.Common;
using Auth.Api.Constants;
using Auth.Api.Contracts;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetAll(
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
        => Ok(await users.GetPageAsync(page, pageSize, search, ct));

    [HttpGet("{id}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(string id, CancellationToken ct)
    {
        EnsureOwnerOrAdmin(id);
        return Ok(await users.GetByIdAsync(id, ct));
    }

    [HttpPut("{id}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Update(string id, UpdateUserRequest request, CancellationToken ct)
    {
        EnsureOwnerOrAdmin(id);
        return Ok(await users.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await users.DeleteAsync(id, User.GetUserId()!, ct);
        return NoContent();
    }

    [HttpGet("{id}/refresh-tokens")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RefreshTokenDto>>> GetRefreshTokens(string id, CancellationToken ct)
    {
        EnsureOwnerOrAdmin(id);
        return Ok(await users.GetRefreshTokensAsync(id, ct));
    }

    [HttpGet("{id}/roles")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRoles(string id, CancellationToken ct)
        => Ok(await users.GetRolesAsync(id, ct));

    [HttpPost("{id}/roles")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<string>>> AddRole(string id, AssignRoleRequest request, CancellationToken ct)
        => Ok(await users.AddRoleAsync(id, request.Role, ct));

    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveRole(string id, string role, CancellationToken ct)
    {
        await users.RemoveRoleAsync(id, role, User.GetUserId()!, ct);
        return NoContent();
    }

    private void EnsureOwnerOrAdmin(string id)
    {
        if (User.GetUserId() != id && !User.IsInRole(Roles.Admin))
            throw AuthException.Forbidden("You can only access your own data");
    }
}