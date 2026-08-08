using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;

namespace Payments.Features.Captures;

public static class UpdatePaymentMethod
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapPut("/captures/{tripId}/payment-method", async (
                string tripId,
                Body body,
                ISender sender,
                CancellationToken ct) =>
                Results.Ok(await sender.Send(new Request(tripId, body.PaymentMethod), ct)));

        public sealed record Body(string PaymentMethod);
    }

    public sealed record Request(string TripId, string PaymentMethod) : IRequest<Response>;

    public sealed record Response(string Status);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(request => request.PaymentMethod)
                .NotEmpty()
                .MaximumLength(128)
                .WithErrorCode("payments:capture:payment_method:invalid");
        }
    }

    public sealed class RequestHandler(PaymentsDbContext db) : IRequestHandler<Request, Response>
    {
        [Realizes("payments/capture", "declined-capture-is-retryable")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.TripId, out var tripId))
            {
                throw NotFound();
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var declined = await db.CaptureFailures
                .AsNoTracking()
                .AnyAsync(failure => failure.TripId == tripId, ct);
            if (!declined)
            {
                await transaction.RollbackAsync(ct);
                throw NotFound();
            }

            var updated = await db.CaptureIntents
                .Where(intent => intent.TripId == tripId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(intent => intent.PaymentMethod, request.PaymentMethod)
                        .SetProperty(intent => intent.DispatchedAt, (DateTimeOffset?)null),
                    ct);
            if (updated != 1)
            {
                await transaction.RollbackAsync(ct);
                throw NotFound();
            }

            // A decline describes the previous instrument. Retaining it as the current status
            // after replacement would tell the rider the new retry had already failed.
            await db.CaptureFailures
                .Where(failure => failure.TripId == tripId)
                .ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            return new Response("pending");
        }

        private static NotFoundException NotFound() =>
            new(
                "no pending capture for that trip",
                "payments:capture:payment_method:not_found");
    }
}
