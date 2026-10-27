using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi.ErrorHandling;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Message),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed",
                                     "One or more validation errors occurred"),
            DomainException e => (StatusCodes.Status400BadRequest, "Business rule violated", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),

            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
                                  => (StatusCodes.Status409Conflict, "Duplicate review",
                                      "A review from this customer for this menu item already exists"),
            MongoWriteException => (StatusCodes.Status400BadRequest, "Write rejected by database",
                                      "The document was rejected by database validation"),

            MongoConnectionException or TimeoutException
                                  => (StatusCodes.Status503ServiceUnavailable, "Database unavailable",
                                      "The database is temporarily unavailable, try again later"),

            _ => (StatusCodes.Status500InternalServerError, "Internal server error",
                                      "An unexpected error occurred")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception: {Status} {Title}", status, title);
        else
            logger.LogWarning("Request failed: {Status} {ExceptionType}: {Message}",
                status, exception.GetType().Name, exception.Message);

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
        }

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}