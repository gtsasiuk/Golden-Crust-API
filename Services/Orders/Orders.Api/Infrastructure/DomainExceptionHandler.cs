using Microsoft.AspNetCore.Diagnostics;
using Orders.Domain;

namespace Orders.Api.Infrastructure;

// Єдине місце, де винятки перетворюються на HTTP-статуси.
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        // Клієнт сам скасував запит: це не помилка сервера
        if (ex is OperationCanceledException) return true;

        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            BusinessConflictException => (StatusCodes.Status409Conflict, "Business rule conflict"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception");
        else
            logger.LogWarning("{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                // Текст винятку віддаємо лише для наших винятків; для 500 деталі не розкриваємо
                Detail = status == StatusCodes.Status500InternalServerError ? null : ex.Message
            }
        });
    }
}