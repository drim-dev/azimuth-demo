# Verification: payments/capture

## Claim: concurrent-completion-processing
Scope: component
Quantification: invariant
Oracle: direct

Uniqueness is enforced by a storage constraint. An in-memory repository serializes writes and
therefore cannot exhibit the race, so evidence at unit scope would be vacuous — it would pass
against an implementation that has no constraint at all.

## Claim: duplicate-completion-event
Scope: component

Deduplication depends on the same storage constraint. The claim is about what the store permits,
not about what the handler intends.

## Claim: retry-after-transport-failure
Scope: component

The claim covers the case where the first attempt's outcome was never observed. Reproducing it
requires a real client and a real store; a substituted payment client cannot distinguish "not
sent" from "sent, response lost", which is the entire content of the claim.

## Claim: capture-created-on-completion
Strength: proof
Evidence: partial unique index `ux_capture_trip` on `captures(trip_id)`

Recorded because the index is what actually holds the line, not because it discharges this
claim's demonstration requirement. It is the mechanism's evidence, and it is why the component
tests above are checking a real constraint rather than application courtesy.

## Residual: ledger-conservation
No evidence that captures, payouts and fees sum correctly across the system. Concern C8, whose
domain is aggregate state over time and whose only honest evidence is a reconciliation job in
production.
Accepted: outside the steel thread. Revisit when payouts exist — until there is a second side to
the ledger, there is nothing to conserve.
