using Azimuth.Annotations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;

namespace Payments.Features.Captures;

/// <summary>The refusals recorded against a trip, oldest first.</summary>
public static class GetCaptureFailures
{
    public sealed record Request(long TripId) : IRequest<IReadOnlyList<string>>;

    public sealed class RequestHandler(PaymentsDbContext db)
        : IRequestHandler<Request, IReadOnlyList<string>>
    {
        [Realizes("payments/capture", "declined-capture-recorded")]
        public async Task<IReadOnlyList<string>> Handle(Request request, CancellationToken ct) =>
            await db.CaptureFailures
                .AsNoTracking()
                .Where(f => f.TripId == request.TripId)
                .OrderBy(f => f.Id)
                .Select(f => f.Reason)
                .ToListAsync(ct);
    }
}
