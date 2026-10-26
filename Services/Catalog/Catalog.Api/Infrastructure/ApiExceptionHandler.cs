using Catalog.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Infrastructure;

public class ApiExceptionHandler(
	IProblemDetailsService problemDetails,
	ILogger<ApiExceptionHandler> logger,
	IHostEnvironment env) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
	{
		if (ex is OperationCanceledException && ct.IsCancellationRequested) return true;

		var (status, title) = ex switch
		{
			EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
			ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
			ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
			DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
			DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent modification"),
			DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }
										 => (StatusCodes.Status409Conflict, "Duplicate value"),
			_ => (StatusCodes.Status500InternalServerError, "Unexpected error")
		};

		if (status >= 500)
			logger.LogError(ex, "Unhandled exception on {Method} {RequestPath}", http.Request.Method, http.Request.Path);
		else
			logger.LogWarning("Request {Method} {RequestPath} rejected with {StatusCode}: {Reason}",
				http.Request.Method, http.Request.Path, status, ex.Message);

		var detail = ex switch
		{
			EntityNotFoundException or ConflictException or DomainException or ValidationException => ex.Message,
			DbUpdateConcurrencyException => "The record was modified by another request. Reload it and try again.",
			DbUpdateException when status == StatusCodes.Status409Conflict => "A record with the same unique value already exists.",
			_ => env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred."
		};

		var context = new ProblemDetailsContext
		{
			HttpContext = http,
			Exception = ex,
			ProblemDetails = new ProblemDetails
			{
				Status = status,
				Title = title,
				Detail = detail,
				Instance = http.Request.Path
			}
		};

		if (ex is ValidationException validation)
			context.ProblemDetails.Extensions["errors"] = validation.Errors
				.GroupBy(e => e.PropertyName)
				.ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

		http.Response.StatusCode = status;
		return await problemDetails.TryWriteAsync(context);
	}
}