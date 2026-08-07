# Verification: payments/capture

## Claim: capture-created-on-completion
Scope: e2e
Quantification: example
Residual: real-process composition is sampled once rather than ranged across all fares and
currencies at e2e scope
Accepted: component evidence ranges over amounts and currencies; the e2e case exists to establish
the process handoff and carries the non-zero surge mutation

The real-process test completes a trip, dispatches the transactional intent and observes the
capture. Component evidence separately ranges over amounts and currencies from intent onward.

## Claim: capture-equals-trip-fare
Scope: component
Quantification: universal
Oracle: model-based

Payments receives only the signed token, independently sums it and persists the provider amount.
Amount/currency variation and altered-token refusal make trusting a forwarded constant or omitting
surge fail. The e2e composition example carries a non-zero surge from Pricing through Trips.

## Claim: concurrent-completion-processing
Scope: component
Quantification: universal
Oracle: direct

Uniqueness is enforced by a storage constraint. An in-memory repository serializes writes and
therefore cannot exhibit the race, so evidence at unit scope would be vacuous — it would pass
against an implementation that has no constraint at all.

## Claim: no-capture-before-completion
Scope: e2e
Quantification: example
Residual: one live trip state is sampled rather than every non-terminal state
Accepted: the process boundary and absence of a premature outbox row are the uncertainty; lifecycle
evidence separately enumerates legal states

The test dispatches Payments before the trip completes and observes no capture for that trip.

## Claim: no-capture-on-cancellation-without-fee
Scope: e2e
Quantification: example
Residual: one no-fee cancellation path is sampled
Accepted: cancellation fees are outside this change; revisit when a fee-producing path exists

The test actually cancels through Trips, dispatches Payments and observes no capture.

## Claim: duplicate-completion-event
Scope: component

Deduplication depends on the same storage constraint. The claim is about what the store permits,
not about what the handler intends.

## Claim: retry-after-transport-failure
Scope: component

The claim covers the case where the first attempt's outcome was never observed. Reproducing it
requires a real client and a real store; a substituted payment client cannot distinguish "not
sent" from "sent, response lost", which is the entire content of the claim.

## Residual: partial-proof-of-uniqueness
Accepted: the tests above carry the claims; this note exists so that the index is not mistaken for
a discharge of them

`ux_capture_trip` proves *at most one* capture per trip, and the claims above say *exactly one*.
The proof covers one half. Declaring it as proof-strength evidence for those claims would let the
at-least-one half pass unverified, so it is recorded on the mechanism side
(`design/payments/capture.md`) and not here.

## Residual: ledger-conservation
Accepted: outside the steel thread; revisit when payouts exist — until there is a second side to
the ledger there is nothing to conserve

No evidence that captures, payouts and fees sum correctly across the system. Concern C8, whose
domain is aggregate state over time and whose only honest evidence is a reconciliation job in
production.
