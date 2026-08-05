using Azimuth.Annotations;
using Pricing;
using Trip.Domain;
using Trip.Storage;

namespace Trip;

/// <summary>
/// The rider- and driver-facing surface of the trip service.
/// </summary>
/// <remarks>
/// Handlers are thin on purpose: the rules live in the domain and in the storage constraints, and a
/// handler that re-decided anything would be a second place for the rule to live. What the handlers
/// do own is admission — <c>AdmitRideRequest</c> is the sole constructor of a trip.
/// </remarks>
public static class Routes
{
    private static readonly TimeSpan QuoteValidity = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OfferValidity = TimeSpan.FromSeconds(30);

    public static void Map(WebApplication app)
    {
        app.MapPost("/quotes", IssueQuote);
        app.MapGet("/quotes/{id:guid}", GetQuote);
        app.MapPost("/trips", RequestRide);
        app.MapGet("/trips/{id:guid}", GetTripForRider);
        app.MapGet("/trips/{id:guid}/offers", GetOffers);
        app.MapGet("/trips/{id:guid}/driver", GetTripDriver);
        app.MapGet("/drivers/{driverId}/offers/{id:guid}", GetOfferForDriver);
        app.MapGet("/drivers/{driverId}/trips/{id:guid}", GetTripForDriver);
        app.MapPost("/trips/{id:guid}/accept/{driverId}", AcceptOffer);
        app.MapPost("/trips/{id:guid}/start", (Guid id, string actor, TripStore trips, Clock clock) =>
            Transition(id, TripEvent.Start, actor, trips, clock));
        app.MapPost("/trips/{id:guid}/complete", (Guid id, string actor, TripStore trips, Clock clock) =>
            Transition(id, TripEvent.Complete, actor, trips, clock));
        app.MapPost("/trips/{id:guid}/cancel", (Guid id, string actor, TripStore trips, Clock clock) =>
            Transition(id, TripEvent.Cancel, actor, trips, clock));
    }

    public sealed record QuoteRequest(string Pickup, string Dropoff, long BaseMinor, long DistanceMinor, string Currency);

    public sealed record RideRequest(string RiderId, Guid QuoteId);

    /// <summary>
    /// A quote's total is the sum of its components, in integer minor units.
    /// </summary>
    /// <remarks>
    /// There is no unserviceable-area rule yet beyond an empty pickup; the claim exists and this is
    /// where it will land.
    /// </remarks>
    [Realizes("pricing/quote", "quote-returned")]
    [Realizes("pricing/quote", "unserviceable-area")]
    [Realizes("pricing/quote", "total-equals-components")]
    [Realizes("pricing/quote", "breakdown-accompanies-quote")]
    private static async Task<IResult> IssueQuote(QuoteRequest request, QuoteStore quotes, Clock clock)
    {
        if (string.IsNullOrWhiteSpace(request.Pickup))
        {
            return Results.BadRequest(new { error = "unserviceable-area" });
        }

        var components = new[]
        {
            Money.Of(request.BaseMinor, request.Currency),
            Money.Of(request.DistanceMinor, request.Currency),
        };
        var total = Money.Sum(request.Currency.ToUpperInvariant(), components);
        var quote = await quotes.IssueAsync(request.Pickup, request.Dropoff, total, QuoteValidity, clock.Now);

        return Results.Ok(new
        {
            id = quote.Id,
            totalMinor = total.MinorUnits,
            currency = total.Currency,
            expiresAt = quote.ExpiresAt,
            breakdown = new[]
            {
                new { label = "base", amountMinor = request.BaseMinor },
                new { label = "distance", amountMinor = request.DistanceMinor },
            },
        });
    }

    [Realizes("pricing/quote", "quote-valid-before-expiry")]
    [Realizes("pricing/quote", "quote-invalid-after-expiry")]
    [Realizes("pricing/quote", "expired-quote-is-never-revalidated")]
    private static async Task<IResult> GetQuote(Guid id, QuoteStore quotes, Clock clock)
    {
        var quote = await quotes.FindAsync(id);
        if (quote is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            id = quote.Id,
            totalMinor = quote.Total.MinorUnits,
            currency = quote.Total.Currency,
            expired = quote.ExpiresAt <= clock.Now,
        });
    }

    /// <summary>
    /// The sole constructor of a trip: resolves the quote and rejects on absence or expiry before
    /// any write, then fans the trip out to available drivers.
    /// </summary>
    [Realizes("trip/request", "request-admitted-with-valid-quote")]
    [Realizes("trip/request", "request-rejected-with-expired-quote")]
    [Realizes("trip/request", "request-rejected-with-unknown-quote")]
    [Realizes("trip/request", "quote-consumed-once")]
    [Realizes("trip/request", "trip-created-in-requested-state")]
    [Realizes("trip/request", "rider-informed-of-trip")]
    [Realizes("trip/request", "second-request-rejected-while-active")]
    [Realizes("trip/request", "request-admitted-after-terminal")]
    private static async Task<IResult> RequestRide(
        RideRequest request,
        TripStore trips,
        OfferStore offers,
        Clock clock)
    {
        var admission = await trips.AdmitAsync(request.RiderId, request.QuoteId, clock.Now);
        if (!admission.Ok)
        {
            return Results.BadRequest(new { error = admission.Reason });
        }

        var offered = await offers.FanOutAsync(admission.TripId, "downtown", clock.Now, OfferValidity);

        return Results.Ok(new
        {
            tripId = admission.TripId,
            state = "requested",
            fareMinor = admission.Fare.MinorUnits,
            currency = admission.Fare.Currency,
            awaitingDriver = true,
            driversOffered = offered,
        });
    }

    /// <summary>
    /// The rider's view of a trip. Everything the rider may see about a driver passes through
    /// <see cref="RiderProjection.For"/>; nothing here decides visibility.
    /// </summary>
    [Realizes("trip/rider-view", "no-driver-identity-before-assignment")]
    [Realizes("trip/rider-view", "no-driver-position-before-assignment")]
    [Realizes("trip/rider-view", "supply-density-shown-before-assignment")]
    [Realizes("trip/rider-view", "driver-shown-after-assignment")]
    [Realizes("trip/rider-view", "driver-position-follows-driver")]
    [Realizes("trip/rider-view", "no-position-after-completion")]
    [Realizes("trip/rider-view", "no-position-after-cancellation")]
    [Realizes("trip/rider-view", "driver-identity-remains-on-receipt")]
    [Realizes("trip/rider-view", "position-confined-to-live-phases")]
    private static async Task<IResult> GetTripForRider(Guid id, TripStore trips)
    {
        var trip = await trips.FindAsync(id);
        if (trip is null)
        {
            return Results.NotFound();
        }

        var driver = trip.AssignedDriverId is null ? null : await trips.DriverAsync(trip.AssignedDriverId);

        return Results.Ok(RiderProjection.For(
            trip.Id,
            trip.State,
            trip.Fare,
            driver?.Display,
            driver?.Vehicle,
            DriverPosition.From(driver?.Position),
            supplyDensity: "moderate"));
    }

    /// <summary>The assigned driver's display record for a trip. Carries no position.</summary>
    /// <remarks>
    /// This route was written to give the receipt's map thumbnail coordinates, by reaching past the
    /// projection for the stored record. The invariant caught it: a rider-facing site that hands
    /// out a raw position discharges nothing, whatever phase the trip is in.
    /// <para>
    /// The thumbnail uses the trip's pickup and dropoff, which are the rider's own data. The
    /// driver's position was never the right source for it.
    /// </para>
    /// </remarks>
    [Realizes("trip/rider-view", "driver-identity-remains-on-receipt")]
    [Realizes("trip/rider-view", "position-confined-to-live-phases")]
    private static async Task<IResult> GetTripDriver(Guid id, TripStore trips)
    {
        var trip = await trips.FindAsync(id);
        if (trip?.AssignedDriverId is null)
        {
            return Results.NotFound();
        }

        var driver = await trips.DriverAsync(trip.AssignedDriverId);
        return driver is null
            ? Results.NotFound()
            : Results.Ok(new { driver.Id, driver.Display, driver.Vehicle });
    }

    [Realizes("trip/driver-view", "pickup-shown-on-offer")]
    [Realizes("trip/driver-view", "rider-contact-hidden-on-offer")]
    [Realizes("trip/driver-view", "rider-contact-confined-to-held-trips")]
    private static async Task<IResult> GetOfferForDriver(string driverId, Guid id, TripStore trips)
    {
        var trip = await trips.FindAsync(id);
        return trip is null
            ? Results.NotFound()
            : Results.Ok(DriverProjection.Offer(trip.Id, "downtown", trip.Fare));
    }

    [Realizes("trip/driver-view", "proxy-contact-while-held")]
    [Realizes("trip/driver-view", "contact-withdrawn-after-terminal")]
    [Realizes("trip/driver-view", "rider-contact-hidden-on-offer")]
    [Realizes("trip/driver-view", "rider-contact-confined-to-held-trips")]
    private static async Task<IResult> GetTripForDriver(string driverId, Guid id, TripStore trips)
    {
        var trip = await trips.FindAsync(id);
        if (trip is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(DriverProjection.For(
            trip.Id,
            trip.State,
            "downtown",
            trip.Fare,
            heldByThisDriver: trip.AssignedDriverId == driverId,
            RiderContact.Of($"proxy:{trip.RiderId}")));
    }

    [Realizes("trip/dispatch", "offer-sent-to-available-nearby-driver")]
    [Realizes("trip/dispatch", "unavailable-driver-not-offered")]
    [Realizes("trip/dispatch", "no-available-drivers")]
    [Realizes("trip/dispatch", "other-offers-withdrawn")]
    [Realizes("trip/dispatch", "expired-offer-withdrawn")]
    private static async Task<IResult> GetOffers(Guid id, OfferStore offers, Clock clock)
    {
        await offers.WithdrawExpiredAsync(clock.Now);
        var rows = await offers.ForTripAsync(id);
        return Results.Ok(rows.Select(o => new { driverId = o.DriverId, state = o.State }));
    }

    [Realizes("trip/dispatch", "first-acceptance-assigns")]
    [Realizes("trip/dispatch", "concurrent-acceptances-yield-one-assignment")]
    [Realizes("trip/dispatch", "late-acceptance-rejected")]
    private static async Task<IResult> AcceptOffer(
        Guid id,
        string driverId,
        OfferStore offers,
        Clock clock)
    {
        var won = await offers.AcceptAsync(id, driverId, clock.Now);
        return won
            ? Results.Ok(new { assigned = true })
            : Results.Conflict(new { assigned = false, error = "offer-taken" });
    }

    [Realizes("trip/lifecycle", "assigned-to-in-progress")]
    [Realizes("trip/lifecycle", "in-progress-to-completed")]
    [Realizes("trip/lifecycle", "unpermitted-transition-rejected")]
    [Realizes("trip/lifecycle", "no-transition-out-of-terminal")]
    [Realizes("trip/lifecycle", "rider-cancels-before-start")]
    [Realizes("trip/lifecycle", "driver-cancels-after-assignment")]
    [Realizes("trip/lifecycle", "cancellation-after-completion-rejected")]
    private static async Task<IResult> Transition(
        Guid id,
        TripEvent @event,
        string actor,
        TripStore trips,
        Clock clock)
    {
        var result = await trips.ApplyAsync(id, @event, actor, clock.Now);
        return result.Ok
            ? Results.Ok(new { state = TripStateMachine.Name(result.To) })
            : Results.Conflict(new { error = result.Reason });
    }
}
