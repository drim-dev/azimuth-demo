# Verification: trips/driver-view

## Claim: rider-contact-hidden-on-offer
Scope: e2e

Composition, as with the rider side: the service can omit the field, the BFF can project a
different model, and the leak lives between them. Every claim in `trips/rider-view` that mattered
turned out to be one no single site established.

## Claim: contact-withdrawn-after-terminal
Scope: e2e

Same composition risk, plus a state boundary — the interesting failure is a projection that was
correct while the trip ran.

## Claim: rider-contact-confined-to-held-trips
Quantification: example
Residual: the class is checked structurally; no test enumerates every driver-facing surface
Accepted: the structural check is what catches a *new* surface, which is what the rider-side leak
showed a behavioural test cannot do. A test that enumerated today's surfaces would restate the
check and rot the day one is added.
