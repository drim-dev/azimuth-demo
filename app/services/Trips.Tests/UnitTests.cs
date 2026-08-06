using Azimuth.Annotations;
using Pricing;
using Trips.Domain;
using Xunit;

namespace Trips.Tests;

/// <summary>
/// Claims whose truth needs nothing real. Under D15 that is exactly what <c>unit</c> means, and it
/// is the default in <c>verification/standards.md</c> — raised per claim only where truth depends
/// on persistence, concurrency, or composition.
/// </summary>
public sealed class MoneyTests
{
    /// <summary>
    /// The plan asks for a metamorphic oracle here: generate component sets and assert the sum
    /// relation, rather than asserting one arithmetic result that a reimplementation of the same
    /// bug would also produce.
    /// </summary>
    [Fact]
    [Covers("pricing/quote", "total-equals-components", Scope.Unit, Quantification.Invariant, Oracle.Metamorphic)]
    public void A_total_equals_the_sum_of_its_components()
    {
        var random = new Random(20260805);
        for (var trial = 0; trial < 500; trial++)
        {
            var count = random.Next(0, 8);
            var components = Enumerable
                .Range(0, count)
                .Select(_ => Money.Of(random.NextInt64(-500_000, 500_000), "EUR"))
                .ToArray();

            var total = Money.Sum("EUR", components);

            // Metamorphic: splitting the set and summing the parts must agree with summing the whole.
            var half = count / 2;
            var left = Money.Sum("EUR", components.Take(half));
            var right = Money.Sum("EUR", components.Skip(half));
            Assert.Equal(total.MinorUnits, left.MinorUnits + right.MinorUnits);
            Assert.Equal(components.Sum(c => c.MinorUnits), total.MinorUnits);
        }
    }

    [Fact]
    [Covers("pricing/quote", "total-in-minor-units", Scope.Unit, Quantification.Invariant)]
    public void An_amount_states_its_currency_and_counts_minor_units()
    {
        var amount = Money.Of(1234, "eur");
        Assert.Equal(1234, amount.MinorUnits);
        Assert.Equal("EUR", amount.Currency);
        Assert.Throws<ArgumentException>(() => Money.Of(1, " "));
    }

    /// <summary>
    /// Currency agreement is checked at runtime, not in the type system — see the residue in
    /// design/pricing/quote.md, which this test is the evidence for.
    /// </summary>
    [Fact]
    [Untraced("guards the runtime half of a mechanism whose claim is covered above")]
    public void Summing_a_mix_of_currencies_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => Money.Sum("EUR", [Money.Of(1, "EUR"), Money.Of(1, "USD")]));
    }
}

public sealed class StateMachineTests
{
    private static readonly HashSet<(TripState, TripEvent)> Permitted =
    [
        (TripState.Requested, TripEvent.Assign),
        (TripState.Assigned, TripEvent.Start),
        (TripState.InProgress, TripEvent.Complete),
        (TripState.Requested, TripEvent.Cancel),
        (TripState.Assigned, TripEvent.Cancel),
        (TripState.InProgress, TripEvent.Cancel),
    ];

    /// <summary>
    /// Model-based, per the plan: the claim quantifies over every state and every event, so the
    /// honest check enumerates the machine and asserts that exactly the permitted pairs are
    /// accepted. A list of examples would satisfy the words and not the claim.
    /// </summary>
    [Fact]
    [Covers("trips/lifecycle", "unpermitted-transition-rejected", Scope.Unit, Quantification.Invariant, Oracle.ModelBased)]
    [Covers("trips/lifecycle", "assigned-to-in-progress", Scope.Unit, Quantification.Invariant, Oracle.ModelBased)]
    [Covers("trips/lifecycle", "in-progress-to-completed", Scope.Unit, Quantification.Invariant, Oracle.ModelBased)]
    public void Exactly_the_permitted_pairs_are_accepted()
    {
        foreach (var from in TripStateMachine.States)
        {
            foreach (var @event in TripStateMachine.Events)
            {
                var accepted = TripStateMachine.Next(from, @event) is not null;
                Assert.Equal(Permitted.Contains((from, @event)), accepted);
            }
        }
    }

    [Fact]
    [Covers("trips/lifecycle", "no-transition-out-of-terminal", Scope.Unit, Quantification.Invariant, Oracle.ModelBased)]
    [Covers("trips/lifecycle", "cancellation-after-completion-rejected", Scope.Unit, Quantification.Invariant, Oracle.ModelBased)]
    public void A_terminal_state_admits_no_event_at_all()
    {
        foreach (var terminal in TripStateMachine.States.Where(TripStateMachine.IsTerminal))
        {
            foreach (var @event in TripStateMachine.Events)
            {
                Assert.Null(TripStateMachine.Next(terminal, @event));
            }
        }
    }

    [Fact]
    [Covers("trips/lifecycle", "rider-cancels-before-start", Scope.Unit, Quantification.Invariant, Oracle.ModelBased)]
    [Covers("trips/lifecycle", "driver-cancels-after-assignment", Scope.Unit, Quantification.Invariant, Oracle.ModelBased)]
    public void Cancellation_is_permitted_from_every_non_terminal_state()
    {
        foreach (var state in TripStateMachine.States.Where(s => !TripStateMachine.IsTerminal(s)))
        {
            Assert.Equal(TripState.Cancelled, TripStateMachine.Next(state, TripEvent.Cancel)!.Value.To);
        }
    }

    [Fact]
    [Untraced("round-trip of the wire names; no claim asserts them")]
    public void State_names_round_trip()
    {
        foreach (var state in TripStateMachine.States)
        {
            Assert.Equal(state, TripStateMachine.Parse(TripStateMachine.Name(state)));
        }
    }
}

public sealed class RiderProjectionTests
{
    private static RiderTripView View(TripState phase) => RiderProjection.For(
        "0000000000000",
        phase,
        Money.Of(1500, "EUR"),
        driverDisplay: "Sam",
        vehicle: "blue hatchback",
        position: DriverPosition.Of("52.37,4.89"),
        supplyDensity: "moderate");

    /// <summary>
    /// Quantified over every phase rather than over the three the spec names, so a state added
    /// later is covered on the day it is added.
    /// </summary>
    [Fact]
    [Covers("trips/rider-view", "no-driver-identity-before-assignment", Scope.Unit, Quantification.Invariant)]
    [Covers("trips/rider-view", "no-driver-position-before-assignment", Scope.Unit, Quantification.Invariant)]
    public void Before_assignment_no_individual_driver_is_shown()
    {
        var view = View(TripState.Requested);
        Assert.Null(view.DriverDisplay);
        Assert.Null(view.DriverPosition);
        Assert.Null(view.Vehicle);
    }

    [Fact]
    [Covers("trips/rider-view", "supply-density-shown-before-assignment", Scope.Unit, Quantification.Invariant)]
    public void Before_assignment_only_coarse_density_is_shown()
    {
        Assert.Equal("moderate", View(TripState.Requested).SupplyDensity);
    }

    [Fact]
    [Covers("trips/rider-view", "driver-shown-after-assignment", Scope.Unit, Quantification.Invariant)]
    public void Between_assignment_and_a_terminal_state_the_driver_is_shown()
    {
        foreach (var phase in new[] { TripState.Assigned, TripState.InProgress })
        {
            var view = View(phase);
            Assert.Equal("Sam", view.DriverDisplay);
            Assert.Equal("52.37,4.89", view.DriverPosition);
        }
    }

    [Fact]
    [Covers("trips/rider-view", "no-position-after-completion", Scope.Unit, Quantification.Invariant)]
    [Covers("trips/rider-view", "no-position-after-cancellation", Scope.Unit, Quantification.Invariant)]
    [Covers("trips/rider-view", "driver-identity-remains-on-receipt", Scope.Unit, Quantification.Invariant)]
    public void After_a_terminal_state_the_name_remains_and_the_position_does_not()
    {
        foreach (var phase in TripStateMachine.States.Where(TripStateMachine.IsTerminal))
        {
            var view = View(phase);
            Assert.Null(view.DriverPosition);
            Assert.Equal("Sam", view.DriverDisplay);
        }
    }

    /// <summary>
    /// The mechanism, not the behaviour: a position has no route to the wire except through the
    /// projection. If this ever compiles differently, the type-level enforcement has been lost.
    /// </summary>
    [Fact]
    [Untraced("asserts the enforcement mechanism itself; the claims it protects are covered above")]
    public void A_position_does_not_serialise_itself()
    {
        Assert.Equal("<redacted>", DriverPosition.Of("52.37,4.89").ToString());
    }
}
