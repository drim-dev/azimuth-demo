# Design: trips/dispatch

## Requirement: single-acceptance
Enforcement: constraint
Site: `trips.assigned_driver_id` set by conditional update predicated on it being null; the
affected-row count is the acceptance result

Compare-and-set at the storage layer, not a check-then-write in the handler. Two accepts arriving
together both read null; only the update matters. A distributed lock over the trip was rejected —
it moves the correctness argument into the lock service's availability, and a lock that fails open
under partition produces exactly the double assignment it was bought to prevent.

The losing driver's response is derived from the affected-row count rather than from re-reading,
so there is no window in which a loser is told it won.

## Residue

**Offers are fanned out optimistically and withdrawn afterwards.** A driver can be shown an offer
for a trip that was assigned milliseconds earlier, and will see it disappear. This is deliberate:
the alternative — reserving the trip before offering — serializes dispatch and makes the common
case slower for a race that resolves in the driver's favour anyway. The UI is expected to absorb
this, and `late-acceptance-rejected` is the claim that keeps it honest.

**There is no fairness guarantee.** Nothing ensures the driver who has waited longest gets the
offer, or that a driver who consistently loses races is compensated. That is a product decision
that has not been made, not an oversight — and it is the kind of thing that becomes a regulatory
question in some markets. Recorded so that whoever adds a scoring function knows they are the
first person to make this decision.
