# Verification: trips/driver-view

## Claim: rider-contact-hidden-on-offer
Scope: e2e
Strength: proof
Evidence: `RiderContact` has no serializer and no accessor returning its raw value;
`DriverProjection.For(held)` is its only reveal, and no driver-facing route returns a rider record

Composition, as with the rider side: the service can omit the field, the BFF can project a
different model, and the leak lives between them. Every claim in `trips/rider-view` that mattered
turned out to be one no single site established.

*(evidence added 2026-08-07)* The e2e is tagged `example` because one offer through the assembled
path is one. What holds the claim everywhere is the type: a contact that cannot be serialized cannot
reach a driver who does not hold the trip, whatever a future route does.
`design/trips/driver-view.md` carries the matching `Enforcement: type`, so this is not proof claimed
out of thin air.

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
