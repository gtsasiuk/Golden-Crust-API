using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Common;

public static class IdentityResultExtensions
{
    public static AuthException ToAuthException(this IdentityResult result, string detail)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        var isDuplicate = result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");

        return isDuplicate
            ? AuthException.Conflict(detail, errors)
            : AuthException.BadRequest(detail, errors);
    }
}