using Microsoft.AspNetCore.Http;

namespace Common.Exceptions;

/// <summary>
/// A failure the domain has a name for, carrying the status and error code it surfaces as.
/// </summary>
/// <remarks>
/// Error codes follow <c>domain:entity:operation:error_type</c>. The code is the stable half of the
/// contract — a client branches on it, where <see cref="Exception.Message"/> is prose that may be
/// reworded — so the codes here are the same strings the specs and the BFF use.
/// </remarks>
public abstract class DomainException(string message, string errorCode, int statusCode)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;

    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status404NotFound);

/// <summary>A state conflict: the caller's view of the world lost a race with another writer.</summary>
public sealed class ConflictException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status409Conflict);

/// <summary>A well-formed request the domain refuses on a rule, not on shape.</summary>
public sealed class BusinessRuleException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status422UnprocessableEntity);
