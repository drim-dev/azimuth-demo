# Design: trip/rider-view

## Requirement: driver-hidden-before-assignment
Enforcement: type
Site: `DriverPosition` has no serializer; it is convertible to a wire model only through
`RiderProjection.For(tripPhase)`, which returns coarse density before assignment

Redaction by construction rather than by remembering. The guard-at-every-site version — each
handler checking the phase before including a position — is the design that leaks, and C1 is in
the catalog precisely because that surface never stops growing.

What this does *not* do is constrain a new endpoint that reaches for the raw position from the
driver service directly. The type protects one path, not the class of all rider-reachable paths.
That gap is the point of the residual in `verification/trip/rider-view.md`.

## Requirement: driver-hidden-after-terminal
Enforcement: type
Site: the same `RiderProjection.For(tripPhase)`, which returns no position for terminal phases
Enforcement: choke-point
Site: `RiderTripStream.Close` is invoked by the state machine's terminal transition, not by the
client disconnecting

The projection covers what a request returns. The stream teardown covers what an already-open
connection keeps pushing, which the projection cannot reach — the interesting failure is a
subscription that was correct while the trip ran and is never torn down.

## Residue

**Coarse supply density is computed from real driver positions and is not differentially
private.** With few drivers in an area, the density signal can identify an individual. This is
accepted for now on the argument that markets launch dense; it is wrong in a market's first week,
which is exactly when nobody is watching. If a launch checklist is ever written, this belongs on
it.

**The rider client caches the last known driver position for offline display.** Terminal-state
teardown clears it, but a client that is killed mid-trip and reopened after completion restores
from cache before the first fetch. The window is small and the claim
`no-position-after-completion` does not currently cover the client's cold start.
