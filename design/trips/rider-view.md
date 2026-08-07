# Design: trips/rider-view

## Requirement: driver-hidden-before-assignment
Enforcement: type
Site: `DriverPosition` has no serializer; it is convertible to a wire model only through
`RiderProjection.For(tripPhase)`, which returns coarse density before assignment

Redaction by construction rather than by remembering. The guard-at-every-site version — each handler
checking the phase before including a position — is the design that leaks, and C1 is in the catalog
precisely because that surface never stops growing.

What this does *not* do is constrain a new endpoint that reaches for the raw position from the
driver service directly. The type protects one path, not the class of all rider-reachable paths.
That gap is the point of the residual in `verification/trips/rider-view.md`.

## Requirement: driver-hidden-after-terminal
Enforcement: type
Site: the same `RiderProjection.For(tripPhase)`, which returns no position for terminal phases

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
Enforcement: type
Site: `DriverPosition` has no serializer; `RiderProjection.For(tripPhase)` is its only reveal
Enforcement: choke-point
Site: no rider-facing route returns a driver record carrying a position — `GetTripDriver` returns
display and vehicle only

Two mechanisms, because the type alone did not hold. A receipt endpoint reached past the projection
for the stored record, satisfied every behavioural claim in the spec, and leaked a completed trip's
position. The type protected one path; the class of rider-reachable paths was unprotected, and kept
growing.

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
