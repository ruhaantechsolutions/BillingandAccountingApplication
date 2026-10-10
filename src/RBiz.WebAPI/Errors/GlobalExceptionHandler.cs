using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RBiz.Domain.Exceptions;

namespace RBiz.WebAPI.Errors;

/// <summary>
/// Single place where an unhandled exception becomes an HTTP response. Implemented with
/// IExceptionHandler rather than a try/catch middleware: the pipeline calls it only on
/// the failure path, so the happy path carries no extra cost.
///
/// Every response is an RFC 9457 ProblemDetails carrying a traceId, so a user can quote
/// one reference and it can be found in the logs.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// Shown for anything unexpected. The real exception goes to the log, never to the
    /// caller: a stack trace or a database error message tells an attacker about the
    /// internals and means nothing to a legitimate user.
    /// </summary>
    private const string UnexpectedErrorMessage =
        "Something went wrong while processing your request. Please try again, and quote " +
        "the reference below if the problem continues.";

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A cancelled request is the client hanging up, not a server fault. Writing a
        // response is pointless and logging it as an error would be noise.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var problemDetails = BuildProblemDetails(httpContext, exception);

        LogException(httpContext, exception, problemDetails.Status);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    /// <summary>
    /// Maps a domain exception onto the right status code and a message meant for a human.
    /// </summary>
    private static ProblemDetails BuildProblemDetails(HttpContext httpContext, Exception exception)
    {
        var problemDetails = exception switch
        {
            ValidationException validation => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = validation.Message
            },

            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = notFound.Message
            },

            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Request conflicts with existing data",
                Detail = conflict.Message
            },

            // Thrown when a service needs a tenant but the request carried no valid token.
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication required",
                Detail = "A valid access token is required to perform this action."
            },

            // Any other broken business rule. Its message is written for an end user.
            DomainException domain => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request could not be completed",
                Detail = domain.Message
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error",
                Detail = UnexpectedErrorMessage
            }
        };

        problemDetails.Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}";

        // The correlation handle between this response and the log entry for it.
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        // Field-level errors so a client can mark the offending inputs.
        if (exception is ValidationException { Errors.Count: > 0 } validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        return problemDetails;
    }

    private void LogException(HttpContext httpContext, Exception exception, int? statusCode)
    {
        // A 4xx is the caller's mistake and is expected traffic, so it is a warning without
        // a stack trace. A 5xx is ours and gets the full exception.
        if (statusCode is >= 400 and < 500)
        {
            _logger.LogWarning(
                "Request {Method} {Path} rejected with {StatusCode}: {Reason} (traceId {TraceId})",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                exception.Message,
                httpContext.TraceIdentifier);

            return;
        }

        _logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} (traceId {TraceId})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);
    }
}
