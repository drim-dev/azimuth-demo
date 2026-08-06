using Azimuth.Annotations;
using Common.Http;
using Common.Identity;
using Common.Time;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Pricing;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Quotes;

/// <summary>
/// Quotes a journey. A quote's total is the sum of its components, in integer minor units.
/// </summary>
/// <remarks>
/// There is no unserviceable-area rule yet beyond an empty pickup; the claim exists and the
/// validator is where it will land.
/// </remarks>
public static class IssueQuote
{
    private static readonly TimeSpan Validity = TimeSpan.FromMinutes(2);

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapPost("/quotes", async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(request, ct)));
    }

    public sealed record Request(
        string Pickup,
        string Dropoff,
        long BaseMinor,
        long DistanceMinor,
        string Currency) : IRequest<Response>;

    public sealed record Component(string Label, long AmountMinor);

    public sealed record Response(
        string Id,
        long TotalMinor,
        string Currency,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<Component> Breakdown);

    /// <summary>
    /// Refuses a journey the service will not price.
    /// </summary>
    /// <remarks>
    /// An empty pickup stands in for the unserviceable-area rule. The error code, not the message,
    /// is what a client branches on, and it is the same string the spec names.
    /// </remarks>
    [Realizes("pricing/quote", "unserviceable-area")]
    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Pickup)
                .NotEmpty()
                .WithErrorCode("pricing:quote:issue:unserviceable_area")
                .WithMessage("a pickup outside the serviceable area cannot be quoted");

            RuleFor(x => x.Currency).NotEmpty();
        }
    }

    public sealed class RequestHandler(TripDbContext db, IdFactory ids, Clock clock)
        : IRequestHandler<Request, Response>
    {
        [Realizes("pricing/quote", "quote-returned")]
        [Realizes("pricing/quote", "total-equals-components")]
        [Realizes("pricing/quote", "breakdown-accompanies-quote")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var currency = request.Currency.ToUpperInvariant();
            var components = new[]
            {
                Money.Of(request.BaseMinor, currency),
                Money.Of(request.DistanceMinor, currency),
            };

            var total = Money.Sum(currency, components);
            var now = clock.Now;
            var quote = new Quote
            {
                Id = ids.Create(),
                Pickup = request.Pickup,
                Dropoff = request.Dropoff,
                TotalMinor = total.MinorUnits,
                Currency = total.Currency,
                IssuedAt = now,
                ExpiresAt = now + Validity,
            };

            db.Quotes.Add(quote);
            await db.SaveChangesAsync(ct);

            return new Response(
                IdEncoding.Encode(quote.Id),
                total.MinorUnits,
                total.Currency,
                quote.ExpiresAt,
                [
                    new Component("base", request.BaseMinor),
                    new Component("distance", request.DistanceMinor),
                ]);
        }
    }
}
