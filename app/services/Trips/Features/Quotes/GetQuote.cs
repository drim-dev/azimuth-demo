using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;

namespace Trips.Features.Quotes;

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

    public sealed class RequestHandler(TripDbContext db, Clock clock) : IRequestHandler<Request, Response>
    {
        /// <summary>
        /// Reports a quote's validity against the clock, and never revalidates one.
        /// </summary>
        /// <remarks>
        /// Expiry is derived on read rather than written by a sweeper: there is no path that moves
        /// <c>expires_at</c>, so an expired quote cannot become valid again however it is fetched.
        /// </remarks>
        [Realizes("pricing/quote", "quote-valid-before-expiry")]
        [Realizes("pricing/quote", "quote-invalid-after-expiry")]
        [Realizes("pricing/quote", "expired-quote-is-never-revalidated")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            // An id from a URL is untrusted: a malformed one is absence, not a server fault.
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw new NotFoundException("no such quote", "pricing:quote:get:not_found");
            }

            var quote = await db.Quotes
                .AsNoTracking()
                .Where(q => q.Id == id)
                .Select(q => new { q.Id, q.TotalMinor, q.Currency, q.ExpiresAt })
                .FirstOrDefaultAsync(ct);

            if (quote is null)
            {
                throw new NotFoundException("no such quote", "pricing:quote:get:not_found");
            }

            return new Response(
                IdEncoding.Encode(quote.Id),
                quote.TotalMinor,
                quote.Currency,
                quote.ExpiresAt <= clock.Now);
        }
    }
}
