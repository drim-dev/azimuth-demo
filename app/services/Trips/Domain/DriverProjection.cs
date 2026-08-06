using Azimuth.Annotations;
using Pricing;

namespace Trips.Domain;

/// <summary>
/// A rider's contact detail. Like <see cref="DriverPosition"/>, it has no serializer and no
/// property returning its raw value.
/// </summary>
public readonly struct RiderContact
{
    private readonly string _value;

    private RiderContact(string value) => _value = value;

    public static RiderContact Of(string value) => new(value);

    internal string Reveal() => _value;

    public override string ToString() => "<redacted>";
}

public sealed record DriverOfferView(string TripId, string PickupArea, long FareMinor, string Currency);

public sealed record DriverTripView(
    string TripId,
    string State,
    string PickupArea,
    long FareMinor,
    string Currency,
    string? RiderContact);

/// <summary>
/// The driver's side of the projection, deliberately a second type rather than a shared one.
/// </summary>
/// <remarks>
/// The two sides have opposite rules: a rider sees the driver only *after* assignment, a driver
/// sees the rider's contact only *while holding* the trip. A shared projection with a direction
/// flag is one conditional away from returning the wrong side's data.
/// </remarks>
[Realizes("trips/driver-view", "rider-contact-confined-to-held-trips")]
public static class DriverProjection
{
    /// <summary>What a driver sees before accepting: the pickup area and the fare, and no rider.</summary>
    [Realizes("trips/driver-view", "pickup-shown-on-offer")]
    [Realizes("trips/driver-view", "rider-contact-hidden-on-offer")]
    [Realizes("trips/driver-view", "rider-contact-confined-to-held-trips")]
    public static DriverOfferView Offer(string tripId, string pickupArea, Money fare) =>
        new(tripId, pickupArea, fare.MinorUnits, fare.Currency);

    /// <summary>The only reveal of a rider contact, and only while the driver holds the trip.</summary>
    [Realizes("trips/driver-view", "proxy-contact-while-held")]
    [Realizes("trips/driver-view", "contact-withdrawn-after-terminal")]
    [Realizes("trips/driver-view", "rider-contact-hidden-on-offer")]
    [Realizes("trips/driver-view", "rider-contact-confined-to-held-trips")]
    public static DriverTripView For(
        string tripId,
        TripState phase,
        string pickupArea,
        Money fare,
        bool heldByThisDriver,
        RiderContact? contact)
    {
        var live = phase is TripState.Assigned or TripState.InProgress;
        return new DriverTripView(
            tripId,
            TripStateMachine.Name(phase),
            pickupArea,
            fare.MinorUnits,
            fare.Currency,
            RiderContact: heldByThisDriver && live ? contact?.Reveal() : null);
    }
}
