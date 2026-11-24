namespace Auth.Api.Common;

public class AuthException(
    int statusCode,
    string title,
    string? detail = null,
    IDictionary<string, string[]>? errors = null) : Exception(detail ?? title)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static AuthException BadRequest(string detail, IDictionary<string, string[]>? errors = null)
        => new(StatusCodes.Status400BadRequest, "Bad request", detail, errors);

    public static AuthException Unauthorized(string detail)
        => new(StatusCodes.Status401Unauthorized, "Unauthorized", detail);

    public static AuthException Forbidden(string detail)
        => new(StatusCodes.Status403Forbidden, "Forbidden", detail);

    public static AuthException NotFound(string detail)
        => new(StatusCodes.Status404NotFound, "Not found", detail);

    public static AuthException Conflict(string detail, IDictionary<string, string[]>? errors = null)
        => new(StatusCodes.Status409Conflict, "Conflict", detail, errors);

    public static AuthException Locked(string detail)
        => new(StatusCodes.Status423Locked, "Account locked", detail);
}