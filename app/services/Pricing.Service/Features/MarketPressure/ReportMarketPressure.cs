using Common.Http;
using Common.Identity;
using Common.Time;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Pricing.Service.Database;
using Pricing.Service.Domain;

namespace Pricing.Service.Features.MarketPressure;

public static class ReportMarketPressure
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapPost("/market-pressure", async (
                [FromBody] Request request,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(request, ct)));
    }

    public sealed record Request(string Market, int OpenRequests, int AvailableDrivers)
        : IRequest<Response>;

    public sealed record Response(string Id, DateTimeOffset ObservedAt);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Market).NotEmpty();
            RuleFor(x => x.OpenRequests).GreaterThanOrEqualTo(0);
            RuleFor(x => x.AvailableDrivers).GreaterThanOrEqualTo(0);
        }
    }

    public sealed class RequestHandler(PricingDbContext db, IdFactory ids, Clock clock)
        : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var observation = new Domain.MarketPressure
            {
                Id = ids.Create(),
                Market = request.Market.Trim().ToLowerInvariant(),
                OpenRequests = request.OpenRequests,
                AvailableDrivers = request.AvailableDrivers,
                ObservedAt = clock.Now,
            };
            db.MarketPressures.Add(observation);
            await db.SaveChangesAsync(ct);
            return new Response(IdEncoding.Encode(observation.Id), observation.ObservedAt);
        }
    }
}
