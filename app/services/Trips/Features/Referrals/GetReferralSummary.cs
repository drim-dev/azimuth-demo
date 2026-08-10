using Azimuth.Annotations;
using Common.Http;
using Common.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Referrals;

public static class GetReferralSummary
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapPut("/referrals/{riderId}", async (
                string riderId,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(new Request(riderId), ct)));
    }

    public sealed record Request(string RiderId) : IRequest<Response>;

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator() => RuleFor(x => x.RiderId).NotEmpty().MaximumLength(128);
    }

    public sealed record Credit(string Id, long AmountMinor, string Currency, string Status);

    public sealed record Response(
        string ReferralCode,
        string AttributionStatus,
        IReadOnlyList<Credit> Credits);

    public sealed class RequestHandler(TripDbContext db, IdFactory ids) : IRequestHandler<Request, Response>
    {
        [Realizes("referrals/rewards", "referral-summary-explains-state")]
        public async Task<Response> Handle(Request request, CancellationToken cancellation)
        {
            var candidateId = ids.Create();
            var candidateCode = IdEncoding.Encode(candidateId);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO referral_accounts (id, rider_id, code)
                VALUES ({candidateId}, {request.RiderId}, {candidateCode})
                ON CONFLICT (rider_id) DO NOTHING
                """, cancellation);

            var account = await db.ReferralAccounts.AsNoTracking()
                .SingleAsync(x => x.RiderId == request.RiderId, cancellation);
            var attribution = await db.ReferralAttributions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ReferredRiderId == request.RiderId, cancellation);
            var credits = await db.ReferralCredits.AsNoTracking()
                .Where(x => x.BeneficiaryRiderId == request.RiderId)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Select(x => new Credit(
                    IdEncoding.Encode(x.Id),
                    x.AmountMinor,
                    x.Currency,
                    x.State == ReferralCreditState.Available
                        ? "available"
                        : x.State == ReferralCreditState.Reserved ? "reserved" : "used"))
                .ToListAsync(cancellation);

            var attributionStatus = attribution switch
            {
                null => "none",
                { QualificationCaptureId: null } => "pending",
                _ => "qualified",
            };

            return new Response(account.Code, attributionStatus, credits);
        }
    }
}
