using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Common;

public class AuthExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AuthException ex)
            return false;

        httpContext.Response.StatusCode = ex.StatusCode;

        ProblemDetails problem = ex.Errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(ex.Errors);

        problem.Status = ex.StatusCode;
        problem.Title = ex.Title;
        problem.Detail = ex.Detail;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}