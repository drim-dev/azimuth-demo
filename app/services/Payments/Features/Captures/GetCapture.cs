using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;

namespace Payments.Features.Captures;

public static class GetCapture
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/captures/{tripId}", async (string tripId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new Request(tripId), ct)));
    }

    public sealed record Request(string TripId) : IRequest<Response>;

    public sealed record Response(string Id, long AmountMinor, string Currency, string? AdjustmentReason);

    public sealed class RequestHandler(PaymentsDbContext db) : IRequestHandler<Request, Response>
    {
        /// <summary>The live capture for a trip, if the trip has one. A voided row is not one.</summary>
        [Realizes("payments/capture", "no-capture-before-completion")]
        [Realizes("payments/capture", "no-capture-on-cancellation-without-fee")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.TripId, out var tripId))
            {
                throw NotFound();
            }

            var capture = await db.Captures
                .AsNoTracking()
                .Where(c => c.TripId == tripId && !c.Voided)
                .Select(c => new { c.Id, c.AmountMinor, c.Currency, c.AdjustmentReason })
                .FirstOrDefaultAsync(ct);

            if (capture is null)
            {
                throw NotFound();
            }

            return new Response(
                IdEncoding.Encode(capture.Id),
                capture.AmountMinor,
                capture.Currency,
                capture.AdjustmentReason);
        }

        private static NotFoundException NotFound() =>
            new("no capture for that trip", "payments:capture:get:not_found");
    }
}
