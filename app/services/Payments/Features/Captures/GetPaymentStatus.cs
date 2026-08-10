using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;

namespace Payments.Features.Captures;

public static class GetPaymentStatus
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/captures/{tripId}/status", async (
                string tripId,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(new Request(tripId), ct)));
    }

    public sealed record Request(string TripId) : IRequest<Response>;

    public sealed record Response(
        string Status,
        long? OriginalFareMinor,
        long? AmountMinor,
        string? Currency,
        Adjustment? Adjustment,
        string Message);

    public sealed record Adjustment(string Kind, long DeltaMinor, string CreditId);

    public sealed class RequestHandler(PaymentsDbContext db) : IRequestHandler<Request, Response>
    {
        [Realizes("payments/capture", "receipt-explains-payment-state")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.TripId, out var tripId))
            {
                throw new NotFoundException(
                    "no payment status for that trip",
                    "payments:capture:status:not_found");
            }

            var capture = await db.Captures
                .AsNoTracking()
                .Where(item => item.TripId == tripId && !item.Voided)
                .Select(item => new
                {
                    item.OriginalFareMinor,
                    item.ReferralCreditMinor,
                    item.ReferralCreditId,
                    item.AmountMinor,
                    item.Currency,
                })
                .FirstOrDefaultAsync(ct);
            if (capture is not null)
            {
                return new Response(
                    "captured",
                    capture.OriginalFareMinor,
                    capture.AmountMinor,
                    capture.Currency,
                    capture.ReferralCreditId is { } creditId
                        ? new Adjustment(
                            "referral-credit",
                            -capture.ReferralCreditMinor,
                            IdEncoding.Encode(creditId))
                        : null,
                    "Payment captured.");
            }

            var declined = await db.CaptureFailures
                .AsNoTracking()
                .AnyAsync(item => item.TripId == tripId, ct);
            if (declined)
            {
                return new Response(
                    "declined",
                    null,
                    null,
                    null,
                    null,
                    "Payment was declined. It will be retried after the payment method is updated.");
            }

            var pending = await db.CaptureIntents
                .AsNoTracking()
                .AnyAsync(item => item.TripId == tripId && item.DispatchedAt == null, ct);
            return pending
                ? new Response(
                    "pending",
                    null,
                    null,
                    null,
                    null,
                    "Payment is being processed. No action is needed.")
                : new Response("none", null, null, null, null, "No payment is due for this trip.");
        }
    }
}
