using Azimuth.Annotations;
using Pricing;

namespace Trips.Domain;

/// <summary>
/// A driver's precise position. Deliberately has no serializer and no property returning its raw
/// value.
/// </summary>
/// <remarks>
/// Redaction by construction rather than by remembering: the only way to a wire model is
/// <see cref="RiderProjection.For"/>, which takes the trip phase. The guard-at-every-site version —
/// each handler checking the phase before including a position — is the design that leaks, and C1
/// is in the concern catalog precisely because that surface never stops growing.
/// <para>
/// What this does <em>not</em> do is constrain a new endpoint that reaches for the raw position
/// from the driver service directly. The type protects one path, not the class of all
/// rider-reachable paths. That gap is the residual in
/// azimuth/model/trips/rider-view/verification.md, and the
/// steel thread exists partly to find out whether the matrix notices it.
/// </para>
/// </remarks>
public readonly struct DriverPosition
{
    private readonly string _value;

    private DriverPosition(string value) => _value = value;

    public static DriverPosition Of(string value) => new(value);

    public static DriverPosition? From(string? value) =>
        value is null ? null : new DriverPosition(value);

    /// <summary>Only the projection may read this, and only after deciding the phase permits it.</summary>
    internal string Reveal() => _value;

    public override string ToString() => "<redacted>";
}

/// <summary>What a rider is shown about their trip, by phase.</summary>
public sealed record RiderTripView(
    string TripId,
    string State,
    long FareMinor,
    string Currency,
    string? DriverDisplay,
    string? Vehicle,
    string? DriverPosition,
    string? SupplyDensity);

public static class RiderProjection
{
    /// <summary>
    /// The single point at which a driver position may reach a rider.
    /// </summary>
    /// <remarks>
    /// Before assignment the rider sees coarse supply density and no individual. Between assignment
    /// and a terminal state they see the assigned driver and their position. After a terminal state
    /// the display name remains for the receipt and the position does not.
    /// </remarks>
    [Realizes("trips/rider-view", "no-driver-identity-before-assignment")]
    [Realizes("trips/rider-view", "no-driver-position-before-assignment")]
    [Realizes("trips/rider-view", "supply-density-shown-before-assignment")]
    [Realizes("trips/rider-view", "driver-shown-after-assignment")]
    [Realizes("trips/rider-view", "driver-position-follows-driver")]
    [Realizes("trips/rider-view", "no-position-after-completion")]
    [Realizes("trips/rider-view", "no-position-after-cancellation")]
    [Realizes("trips/rider-view", "driver-identity-remains-on-receipt")]
    [Realizes("trips/rider-view", "position-confined-to-live-phases")]
    public static RiderTripView For(
        string tripId,
        TripState phase,
        Money fare,
        string? driverDisplay,
        string? vehicle,
        DriverPosition? position,
        string supplyDensity)
    {
        var assigned = phase is TripState.Assigned or TripState.InProgress;
        var terminal = TripStateMachine.IsTerminal(phase);

        return new RiderTripView(
            tripId,
            TripStateMachine.Name(phase),
            fare.MinorUnits,
            fare.Currency,
            DriverDisplay: assigned || terminal ? driverDisplay : null,
            Vehicle: assigned || terminal ? vehicle : null,
            // The only reveal in the system, and it is unreachable outside the assigned phases.
            DriverPosition: assigned ? position?.Reveal() : null,
            SupplyDensity: assigned || terminal ? null : supplyDensity);
    }
}
