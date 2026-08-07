using Azimuth.Annotations;
using Common.Http;
using Common.Identity;
using Common.Time;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pricing.Service.Database;
using Pricing.Service.Domain;

namespace Pricing.Service.Features.Quotes;

public static class IssueQuote
{
    private static readonly TimeSpan Validity = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PressureFreshness = TimeSpan.FromMinutes(5);
    private const long BaseMinor = 500;
    private const string Policy = "surge-v1";

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapPost("/quotes", async ([FromBody] Request request, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(request, ct)));
    }

    public sealed record Request(string Pickup, string Dropoff, long DistanceMeters, string Currency)
        : IRequest<Response>;

    public sealed record Response(
        string Id,
        string Token,
        long TotalMinor,
        string Currency,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<QuoteComponent> Breakdown);

    [Realizes("pricing/quote", "unserviceable-area")]
    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Pickup)
                .Equal("downtown", StringComparer.OrdinalIgnoreCase)
                .WithErrorCode("pricing:quote:issue:unserviceable_area")
                .WithMessage("a pickup outside the serviceable area cannot be quoted");
            RuleFor(x => x.Dropoff).NotEmpty();
            RuleFor(x => x.DistanceMeters).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Currency).NotEmpty().Length(3);
        }
    }

    public sealed class RequestHandler(
        PricingDbContext db,
        IdFactory ids,
        Clock clock,
        QuoteTokenCodec tokens) : IRequestHandler<Request, Response>
    {
        [Realizes("pricing/quote", "quote-returned")]
        [Realizes("pricing/quote", "total-equals-components")]
        [Realizes("pricing/quote", "current-pressure-selects-surge")]
        [Realizes("pricing/quote", "stale-pressure-does-not-select-surge")]
        [Realizes("pricing/quote", "surge-is-a-quote-component")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var now = clock.Now;
            var market = request.Pickup.Trim().ToLowerInvariant();
            var pressure = await db.MarketPressures
                .AsNoTracking()
                .Where(x => x.Market == market && x.ObservedAt > now - PressureFreshness && x.ObservedAt <= now)
                .OrderByDescending(x => x.ObservedAt)
                .FirstOrDefaultAsync(ct);

            var currency = request.Currency.ToUpperInvariant();
            var distanceMinor = checked(request.DistanceMeters / 10);
            var subtotal = checked(BaseMinor + distanceMinor);
            var surgeMinor = pressure is not null && pressure.OpenRequests > pressure.AvailableDrivers
                ? subtotal / 5
                : 0;
            QuoteComponent[] components =
            [
                new("base", BaseMinor),
                new("distance", distanceMinor),
                new("surge", surgeMinor),
            ];
            var total = Money.Sum(currency, components.Select(x => Money.Of(x.AmountMinor, currency)));
            var id = ids.Create();
            var payload = new QuotePayload(
                id,
                request.Pickup,
                request.Dropoff,
                now,
                now + Validity,
                Policy,
                pressure?.Id,
                currency,
                components,
                total.MinorUnits);
            var token = tokens.Encode(payload);

            db.Quotes.Add(new IssuedQuote
            {
                Id = id,
                Pickup = request.Pickup,
                Dropoff = request.Dropoff,
                TotalMinor = total.MinorUnits,
                Currency = currency,
                IssuedAt = payload.IssuedAt,
                ExpiresAt = payload.ExpiresAt,
                Token = token,
            });
            await db.SaveChangesAsync(ct);

            return new Response(
                IdEncoding.Encode(id), token, total.MinorUnits, currency, payload.ExpiresAt, components);
        }
    }
}
