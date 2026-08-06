using Azimuth.Annotations;
using Pricing;

namespace Trips.Domain;

public enum TripState
{
    Requested,
    Assigned,
    InProgress,
    Completed,
    Cancelled,
}

public enum TripEvent
{
    Assign,
    Start,
    Complete,
    Cancel,
}

public readonly record struct Transition(TripState To)
{
    public static Transition? Rejected => null;
}

/// <summary>
/// The trip state machine: the only place <c>trips.state</c> is decided.
/// </summary>
/// <remarks>
/// A total function from (state, event) to either a new state or a rejection. Encoding the machine
/// in the type system so that illegal pairs are unrepresentable was considered and rejected: it is
/// expressible in C# but not in TypeScript or the mobile client, and a rule that holds in one
/// language of three gives false confidence at the boundaries where trips actually move.
/// <para>
/// The consequence is that <c>unpermitted-transition-rejected</c> carries a model-based oracle
/// rather than being vacuous — the check enumerates the machine.
/// </para>
/// </remarks>
public static class TripStateMachine
{
    public static readonly IReadOnlyList<TripState> States =
    [
        TripState.Requested,
        TripState.Assigned,
        TripState.InProgress,
        TripState.Completed,
        TripState.Cancelled,
    ];

    public static readonly IReadOnlyList<TripEvent> Events =
    [
        TripEvent.Assign,
        TripEvent.Start,
        TripEvent.Complete,
        TripEvent.Cancel,
    ];

    /// <summary>Terminal states are final, by every path.</summary>
    [Realizes("trips/lifecycle", "no-transition-out-of-terminal")]
    [Realizes("trips/lifecycle", "replayed-transition-is-inert")]
    public static bool IsTerminal(TripState state) =>
        state is TripState.Completed or TripState.Cancelled;

    /// <summary>
    /// The permitted transitions, and nothing else.
    /// </summary>
    [Realizes("trips/lifecycle", "assigned-to-in-progress")]
    [Realizes("trips/lifecycle", "in-progress-to-completed")]
    [Realizes("trips/lifecycle", "unpermitted-transition-rejected")]
    [Realizes("trips/lifecycle", "no-transition-out-of-terminal")]
    [Realizes("trips/lifecycle", "rider-cancels-before-start")]
    [Realizes("trips/lifecycle", "driver-cancels-after-assignment")]
    [Realizes("trips/lifecycle", "cancellation-after-completion-rejected")]
    public static Transition? Next(TripState from, TripEvent @event)
    {
        if (IsTerminal(from))
        {
            return Transition.Rejected;
        }

        return (from, @event) switch
        {
            (TripState.Requested, TripEvent.Assign) => new Transition(TripState.Assigned),
            (TripState.Assigned, TripEvent.Start) => new Transition(TripState.InProgress),
            (TripState.InProgress, TripEvent.Complete) => new Transition(TripState.Completed),
            (TripState.Requested, TripEvent.Cancel) => new Transition(TripState.Cancelled),
            (TripState.Assigned, TripEvent.Cancel) => new Transition(TripState.Cancelled),
            (TripState.InProgress, TripEvent.Cancel) => new Transition(TripState.Cancelled),
            _ => Transition.Rejected,
        };
    }

    public static string Name(TripState state) => state switch
    {
        TripState.Requested => "requested",
        TripState.Assigned => "assigned",
        TripState.InProgress => "in-progress",
        TripState.Completed => "completed",
        TripState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    public static TripState Parse(string name) => name switch
    {
        "requested" => TripState.Requested,
        "assigned" => TripState.Assigned,
        "in-progress" => TripState.InProgress,
        "completed" => TripState.Completed,
        "cancelled" => TripState.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown trip state"),
    };
}
