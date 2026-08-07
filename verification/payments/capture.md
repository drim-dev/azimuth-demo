# Verification: payments/capture

## Claim: capture-created-on-completion
Residual: the trigger is not built. Evidence establishes that an intent produces exactly one capture
for its amount; nothing establishes that completing a trip produces an intent, because the trip
service does not write one and `WriteCaptureIntent` is unreachable.
Accepted: no — recorded, not accepted. The chain has two halves and only one is claimed anywhere,
so the gap is in the intent facet before it is in the evidence. Closing it means either a claim in
`trips/lifecycle` that completion emits a capture intent, or building the outbox the design
describes. Until then this claim reads as end-to-end and is not.

## Claim: concurrent-completion-processing
Scope: component
Quantification: universal
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
