# Design: trips/rider-view

## Requirement: driver-hidden-before-assignment
Enforcement: guard
Binding: dotnet-symbol:Trips.Domain.RiderProjection.For

One projection guard rather than a phase check repeated in each page. The route enumerator does not
prove the guard is used; it makes every new surface declare how the rule is discharged, which is the
part a hand-maintained route list cannot do.

What this does *not* do is constrain a new endpoint that reaches for the raw position from the
driver service directly. The projection protects one path, not the class of all rider-reachable
paths. That gap is the point of the residual in `verification/trips/rider-view.md`.

## Requirement: driver-hidden-after-terminal
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Domain.RiderProjection.For

Every rider-facing read of a trip goes through the projection, and after a terminal transition it
returns no position. There is one observation mode and the projection covers it.

*(revised 2026-08-07 — supersedes a second `Enforcement: choke-point` naming
`RiderTripStream.Close`, "invoked by the state machine's terminal transition, not by the client
disconnecting", described as covering what an already-open connection keeps pushing. **No such type
exists.** The fixture has no streaming: the rider page polls with `router.refresh()`, which re-runs
the same projection on the server. The entry described a mechanism for a failure mode this system
cannot have, and the agent tier cited it as evidence for two `spec-gap` verdicts before anyone
checked the code against it.)*

## Requirement: position-confined-to-live-phases
Enforcement: guard
Binding: dotnet-symbol:Trips.Domain.RiderProjection.For
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Trips.GetTripDriver.RequestHandler.Handle

Two mechanisms, because the projection alone did not hold. A receipt endpoint reached past it
for the stored record, satisfied every behavioural claim in the spec, and leaked a completed trip's
position. One guarded path existed while the class of rider-reachable paths was unprotected and
kept growing.

The second mechanism is the negative one: no rider-facing route hands out a position at all, so
there is nothing for a new surface to reach for. That is weaker than it sounds — it is a property of
the routes that exist, and the invariant over the site class is what makes a new route's silence
visible rather than assumed.

Rejected: giving the receipt a redacted position through the projection. It would have worked and it
would have left the raw route in place for the next surface to find.

## Residue

**Coarse supply density is computed from real driver positions and is not differentially private.**
With few drivers in an area, the density signal can identify an individual. This is accepted for now
on the argument that markets launch dense; it is wrong in a market's first week, which is exactly
when nobody is watching. If a launch checklist is ever written, this belongs on it.

*(removed 2026-08-07 — a residue paragraph claimed "the rider client caches the last known driver
position for offline display", leaving a cold-start window the claims did not cover. The client
caches nothing: `lib/trip-service.ts` fetches with `cache: 'no-store'`, and `refresher.tsx` uses
`router.refresh()` specifically so that what the page shows is decided by the same server-side
projection that decided the first render. The residue recorded a risk the system does not have.)*
