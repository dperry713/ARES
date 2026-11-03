using Ares.Execution.Services;
using Ares.Infrastructure.Audit;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ares.Api.Platform;

/// <summary>
/// Single exception boundary for the API. Every failure becomes an RFC 9457
/// problem+json response with a stable type URI, the trace id, and (for policy
/// denials) the machine-readable reason code. No stack trace or internal detail
/// ever reaches a client.
/// </summary>
public sealed class AresExceptionHandler(IAuditLog audit, ILogger<AresExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        var (status, type, title, reasonCode, explanations) = exception switch
        {
            PolicyDeniedException pde => (StatusCodes.Status403Forbidden,
                "https://ares.local/errors/policy-denied", "Denied by policy",
                pde.ReasonCode, pde.Explanations),
            KeyNotFoundException => (StatusCodes.Status404NotFound,
                "https://ares.local/errors/not-found", "Resource not found", null, (IReadOnlyList<string>)[]),
            ArgumentException or ArgumentOutOfRangeException => (StatusCodes.Status400BadRequest,
                "https://ares.local/errors/invalid-request", "Invalid request", null, (IReadOnlyList<string>)[]),
            InvalidOperationException => (StatusCodes.Status409Conflict,
                "https://ares.local/errors/conflict", "Operation conflicts with current state", null, (IReadOnlyList<string>)[]),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,
                "https://ares.local/errors/forbidden", "Forbidden", null, (IReadOnlyList<string>)[]),
            _ => (StatusCodes.Status500InternalServerError,
                "https://ares.local/errors/internal", "An unexpected error occurred", null, (IReadOnlyList<string>)[])
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", http.Request.Method, http.Request.Path);
        else
            logger.LogInformation("Handled {ExceptionType} -> {Status} on {Method} {Path}",
                exception.GetType().Name, status, http.Request.Method, http.Request.Path);

        if (status is 403 or 409)
            audit.Record("policy.denied", "denied", "request", http.Request.Path,
                detail: $"{exception.GetType().Name}: {exception.Message}", subject: http.User.SubjectId());

        if (http.Response.HasStarted) return false;

        http.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = status >= 500 ? null : exception.Message,
            Instance = http.Request.Path
        };
        problem.Extensions["traceId"] = http.TraceIdentifier;
        if (reasonCode is not null) problem.Extensions["reasonCode"] = reasonCode;
        if (explanations.Count > 0) problem.Extensions["explanations"] = explanations;

        await http.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
