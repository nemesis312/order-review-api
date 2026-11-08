using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderReview.Api.Exceptions;

namespace OrderReview.Api.Errors;

/// <summary>Single place that maps exceptions to RFC 7807 responses.</summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499; // client closed the request
            return true;
        }

        var problem = exception switch
        {
            OrderNotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Order not found",
                Detail = notFound.Message
            },
            OrderValidationException validation => new HttpValidationProblemDetails(validation.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            },
            _ => CreateGenericServerError(exception)
        };

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = null // never let the framework add exception details
        });
    }

    private ProblemDetails CreateGenericServerError(Exception exception)
    {
        logger.LogError(exception, "Unhandled exception");
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "The server could not process the request. Please try again later."
        };
    }
}
