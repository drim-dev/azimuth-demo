using FluentValidation;
using MediatR;

namespace Common.Validation;

/// <summary>
/// Runs a request's validators before its handler, so shape is settled in one place.
/// </summary>
/// <remarks>
/// In the pipeline rather than at the endpoint: a handler reached from a test, a background loop or
/// a second endpoint gets the same refusal as one reached over HTTP. Validation here is about the
/// shape of the request; a rule that needs to read storage belongs in the handler, where it can be
/// refused with the domain's own error code.
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellation)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellation))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
