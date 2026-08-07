using Azimuth.Annotations;
using Common.Exceptions;
using Common.Http;
using Common.Identity;
using Common.Time;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Trips.Database;
using Trips.Domain;

namespace Trips.Features.Trips;

/// <summary>
/// Moves a trip through the machine, with a conditional write on the current state.
/// </summary>
/// <remarks>
/// One slice for the three events because the mechanism is one mechanism; splitting it per verb
/// would put the same conditional write in three places and invite them to drift.
/// </remarks>
public static class TransitionTrip
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app)
        {
            Map(app, "start", TripEvent.Start);
            Map(app, "complete", TripEvent.Complete);
            Map(app, "cancel", TripEvent.Cancel);
        }

        private static void Map(WebApplication app, string verb, TripEvent @event) =>
            app.MapPost($"/trips/{{id}}/{verb}", async (
                string id,
                string actor,
                ISender sender,
                CancellationToken ct) =>
            {
                var response = await sender.Send(new Request(id, @event, actor), ct);
                return Results.Ok(response);
            });
    }

    public sealed record Request(string Id, TripEvent Event, string Actor) : IRequest<Response>;

    public sealed record Response(string State);

    public sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator() => RuleFor(x => x.Actor).NotEmpty();
    }

    public sealed class RequestHandler(TripDbContext db, Clock clock) : IRequestHandler<Request, Response>
    {
        /// <summary>
        /// Applies an event through the state machine, against the row it locked.
        /// </summary>
        /// <remarks>
        /// Two mechanisms for one rule. The machine alone does not survive concurrency: two in-flight
        /// handlers can both read <c>in-progress</c>, both pass, and the later write wins. The
        /// conditional update is what makes a replayed transition inert rather than merely unlikely.
        /// </remarks>
        [Realizes("trips/lifecycle", "assigned-to-in-progress")]
        [Realizes("trips/lifecycle", "in-progress-to-completed")]
        [Realizes("trips/lifecycle", "unpermitted-transition-rejected")]
        [Realizes("trips/lifecycle", "no-transition-out-of-terminal")]
        [Realizes("trips/lifecycle", "replayed-transition-is-inert")]
        [Realizes("trips/lifecycle", "transition-records-actor-and-instant")]
        [Realizes("trips/lifecycle", "history-is-append-only")]
        [Realizes("trips/lifecycle", "rider-cancels-before-start")]
        [Realizes("trips/lifecycle", "driver-cancels-after-assignment")]
        [Realizes("trips/lifecycle", "cancellation-after-completion-rejected")]
        [Realizes("payments/capture", "capture-created-on-completion")]
        [Realizes("payments/capture", "no-capture-before-completion")]
        [Realizes("payments/capture", "no-capture-on-cancellation-without-fee")]
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.Id, out var id))
            {
                throw new NotFoundException("no such trip", "trip:trip:transition:not_found");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Locks the row for the duration, so the state read here is the state written against.
            var current = await db.Trips
                .FromSql($"SELECT * FROM trips WHERE id = {id} FOR UPDATE")
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (current is null)
            {
                throw new NotFoundException("no such trip", "trip:trip:transition:not_found");
            }

            var from = current.State;
            if (TripStateMachine.Next(from, request.Event) is not { } next)
            {
                await transaction.RollbackAsync(ct);
                throw new ConflictException(
                    $"a {TripStateMachine.Name(from)} trip does not accept that event",
                    "trip:trip:transition:not_permitted");
            }

            var to = next.To;
            var moved = await db.Trips
                .Where(t => t.Id == id && t.State == from)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.State, to), ct);

            if (moved != 1)
            {
                await transaction.RollbackAsync(ct);
                throw new ConflictException(
                    "the trip moved while this transition was in flight",
                    "trip:trip:transition:state_moved");
            }

            db.TripTransitions.Add(new TripTransition
            {
                TripId = id,
                FromState = from,
                ToState = to,
                Actor = request.Actor,
                OccurredAt = clock.Now,
            });

            if (to == TripState.Completed)
            {
                // The intent commits with the state change. A direct payment call here could
                // charge before a transaction that later rolls back.
                db.CaptureIntents.Add(new CaptureIntent
                {
                    TripId = id,
                    QuoteToken = current.QuoteToken,
                    WrittenAt = clock.Now,
                });
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new Response(TripStateMachine.Name(to));
        }
    }
}
