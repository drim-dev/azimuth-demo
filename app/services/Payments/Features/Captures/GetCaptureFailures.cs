using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Database;

namespace Payments.Features.Captures;

/// <summary>The refusals recorded against a trip, oldest first.</summary>
public static class GetCaptureFailures
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app) =>
            app.MapGet("/captures/{tripId}/failures", async (
                string tripId,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(new Request(tripId), ct)));
    }

    public sealed record Request(string TripId) : IRequest<IReadOnlyList<string>>;

    public sealed class RequestHandler(PaymentsDbContext db)
        : IRequestHandler<Request, IReadOnlyList<string>>
    {
        [Realizes("payments/capture", "declined-capture-recorded")]
        public async Task<IReadOnlyList<string>> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.TripId, out var tripId))
            {
                throw new NotFoundException("no such trip", "payments:capture:list_failures:not_found");
            }

            return await db.CaptureFailures
                .AsNoTracking()
                .Where(f => f.TripId == tripId)
                .OrderBy(f => f.Id)
                .Select(f => f.Reason)
                .ToListAsync(ct);
        }
    }
}
