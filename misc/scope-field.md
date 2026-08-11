# Does the `Scope` field earn its place?

**Status: two observations and an open question. No proposal.** From working through
`azimuth/model/trips/request/verification.md` on 2026-08-07. This points the opposite way from most
of `misc/`: it is evidence that an *existing* field may not be doing work, not an argument for a
new one.

## Observation 1 — the harness decided the scope, not the claims

All eight covering tests in `app/services/Trips.Tests/Features/Trips/RequestRideTests.cs` declare
`Scope.Component`. Five of those claims have no plan entry, so the standard applies and their
required scope is `unit`.

Nothing fires, and nothing should: `Scope` is a ladder (`unit < component < e2e`) and a stronger
form satisfies a weaker requirement. The tags are also *true* — `TripTestFixture` runs real Postgres
and speaks HTTP, so these are component tests by any definition. **The problem is not that the tags
are false. It is that they are uninformative.** They record a property of the test infrastructure,
because the fixture is component-shaped and everything written in it inherits that shape.

This matters against `standards.md`'s own argument. That document rejects "critical implies
component" as the C5 mistake — *"an authorization rule is critical and honestly unit-checkable, and
forcing it to component scope buys nothing while making the requirement look arbitrary"* — and says
what determines scope is what the claim *is about*. Here the same collapse arrives from the
infrastructure side rather than the policy side, and over-declaration is always legal, so no check
can see it.

The consequence is concrete: `standards.md` says a plan entry raising scope "marks something
specific and reviewable… there should not be many of them". `trips/request` has three such entries.
In this corpus they distinguish nothing observable, because the claims without entries are verified
exactly the same way.

**The fair counter-argument.** The fixture is component-shaped for a stated reason —
`Trips.Tests/Fixtures/Api.cs`: *"a component test that called a handler would pass against a slice
whose endpoint was never wired up."* A project-wide preference for realistic tests is defensible.
But then the field is recording a project-level decision, not a per-claim one, and that should be
said out loud in `standards.md` rather than left to be inferred from every tag having the same
value.

## Observation 2 — a mutation can derive the required scope

`request-admitted-after-terminal` had no plan entry and so inherited `unit`. Its mechanism is
`ux_trip_rider_active`, a partial unique index whose filter enumerates the terminal states.

The mutation that breaks it is a **migration** edit — removing `'completed'` from the filter — and
it was run to test whether the rewritten evidence had teeth. It also settled a different question: a
fault that only a real store can catch is evidence that the claim's truth depends on a real store,
which is exactly `standards.md`'s criterion for raising scope.

So mutation, which the framework currently treats as a way of checking evidence, is also a way of
*deriving a required form*. The entry has since been added to the plan, marked. This is one
instance; whether it generalizes is unknown, and the obvious limit is that it only works in the
direction of raising — a mutation that a unit test catches proves nothing about whether component
scope was needed elsewhere.

## The open question

**Has any plan entry raising scope ever changed what evidence got written?**

Two counts would answer it, and both are currently zero as far as this session could tell:

1. Claims where covering tests declare *different* scopes, so the field discriminates between them.
2. Claims where a plan entry's raise is what caused the evidence to be written at that scope, rather
   than the harness making it so anyway.

If both stay zero across the four unjudged specs, the field is recording a constant, and D13's own
argument applies to it — *a field whose value never varies carries no information* — which is the
reasoning that deleted the claim-side quantifier. That would be an argument for removing `Scope`
from the tag and keeping it only in the plan, where it states a requirement rather than a fact. Not
proposed here: one spec is not evidence, and `UnitTests.cs` does carry `Scope.Unit` tags, so the
corpus-wide answer is not obviously zero.

## Related — plan hygiene in the same file

The `request-rejected-with-unknown-quote` entry in
`azimuth/model/trips/request/verification.md` now carries **no fields**. It was added on 2026-08-07
as a revision marker after the `Oracle: contract` requirement
turned out to describe a service boundary that does not exist. Per the format, an entry states only
what it changes, so an entry that changes nothing is prose in a file meant to hold deviations —
`azimuth/formats/verification.md`'s "plan being used as an inventory" failure, in miniature.

It is defensible under "mark revisions; do not silently rewrite", and the finding is also recorded
permanently in `azimuth/model/trips/request/judgments.md`. Once that record is trusted, deleting the
entry is the tidier call.
