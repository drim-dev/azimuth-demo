using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pricing.Service.Database;

namespace Pricing.Service.Features.Quotes;

public static class GetQuote
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/quotes/{id}", async (string id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new Request(id), ct)));
    }

    public sealed record Request(string Id) : IRequest<Response>;
    public sealed record Response(string Id, long TotalMinor, string Currency, bool Expired);

    public sealed class RequestHandler(PricingDbContext db, Clock clock) : IRequestHandler<Request, Response>
    {
        [Realizes("pricing/quote", "quote-valid-before-expiry")]
        [Realizes("pricing/quote", "quote-invalid-after-expiry")]
        [Realizes("pricing/quote", "expired-quote-is-never-revalidated")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw Missing();
            }

            var quote = await db.Quotes.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.TotalMinor, x.Currency, x.ExpiresAt })
                .FirstOrDefaultAsync(ct);
            if (quote is null)
            {
                throw Missing();
            }

            return new Response(
                IdEncoding.Encode(quote.Id), quote.TotalMinor, quote.Currency, quote.ExpiresAt <= clock.Now);
        }

        private static NotFoundException Missing() =>
            new("no such quote", "pricing:quote:get:not_found");
    }
}
