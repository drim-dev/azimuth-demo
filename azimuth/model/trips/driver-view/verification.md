# Verification: trips/driver-view

## Claim: rider-contact-hidden-on-offer
Scope: e2e
Quantification: example
Residual: the e2e exercises one offer surface, while the type has an assembly-internal `Reveal()`
escape hatch and therefore cannot establish proof
Accepted: `DriverProjection.Offer` returns a model with no rider field and the agent checks the
derived driver surface. Revisit when egress analysis can prove every driver-facing serializer.

Composition, as with the rider side: the service can omit the field, the BFF can project a
different model, and the leak lives between them. Every claim in `trips/rider-view` that mattered
turned out to be one no single site established.

*(revised 2026-08-08)* The e2e is tagged `example` because one offer through the assembled path is
one. The earlier proof claim was false: `RiderContact.Reveal()` is internal rather than
inaccessible. The design now records the projection guard at the form the code actually has.

## Claim: contact-withdrawn-after-terminal
Scope: e2e

Same composition risk, plus a state boundary — the interesting failure is a projection that was
correct while the trip ran.

## Claim: pickup-shown-on-offer
Quantification: example
Residual: one offer shape is exercised — one pickup area, one fare, one driver. Nothing ranges over
offers.
Accepted: the claim is that the pickup and fare *are* shown, and a type cannot carry a positive
claim the way it carries the hiding ones. Ranging over offer shapes would exercise the fixture's
single pickup area repeatedly and learn nothing. Revisit when the fixture has more than one market.

## Claim: rider-contact-confined-to-held-trips
Quantification: example
Residual: the class is checked structurally; no test enumerates every driver-facing surface
Accepted: the structural check is what catches a *new* surface, which is what the rider-side leak
showed a behavioural test cannot do. A test that enumerated today's surfaces would restate the
check and rot the day one is added.
