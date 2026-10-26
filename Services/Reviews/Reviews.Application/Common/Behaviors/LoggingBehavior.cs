using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", name, request);

        try
        {
            var response = await next();
            logger.LogInformation("Handled {RequestName}", name);
            return response;
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("{RequestName} rejected by validation before the handler ran: {Errors}",
                name, ex.Errors.Select(e => e.ErrorMessage));
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{RequestName} failed: {ExceptionType}", name, ex.GetType().Name);
            throw;
        }
    }
}