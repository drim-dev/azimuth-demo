using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Common.Exceptions;

/// <summary>
/// Turns domain and validation failures into ProblemDetails, so every service answers a refusal in
/// one shape.
/// </summary>
/// <remarks>
/// Handlers throw and do not format. A handler that built its own error body would be a second
/// place the wire contract lives, and the BFF forwards these responses unchanged.
/// </remarks>
public sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellation)
    {
        var problem = exception switch
        {
            ValidationException validation => ValidationProblem(validation, context),
            DomainException domain => DomainProblem(domain, context),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        // Refusals are the domain working, not the service failing; only the unhandled path is an error.
        logger.LogInformation(
            "{Path} refused: {ErrorCode}",
            context.Request.Path,
            problem.Extensions["errorCode"]);

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, cancellation);
        return true;
    }

    private static ProblemDetails DomainProblem(DomainException exception, HttpContext context)
    {
        var problem = Problem(exception.StatusCode, exception.Message, context);
        problem.Extensions["errorCode"] = exception.ErrorCode;
        return problem;
    }

    private static ProblemDetails ValidationProblem(ValidationException exception, HttpContext context)
    {
        var failure = exception.Errors.First();
        var problem = Problem(StatusCodes.Status400BadRequest, failure.ErrorMessage, context);

        // The rule name carries the domain's own vocabulary for the refusal; see the validators.
        problem.Extensions["errorCode"] = failure.ErrorCode ?? "validation:request:validate:invalid";
        problem.Extensions["errors"] = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return problem;
    }

    private static ProblemDetails Problem(int status, string detail, HttpContext context) => new()
    {
        Type = "https://tools.ietf.org/html/rfc7807",
        Title = ReasonPhrases.Of(status),
        Status = status,
        Detail = detail,
        Instance = context.Request.Path,
        Extensions = { ["traceId"] = context.TraceIdentifier },
    };

    private static class ReasonPhrases
    {
        public static string Of(int status) => status switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
            _ => "Error",
        };
    }
}
