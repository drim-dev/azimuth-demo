# Verification: trips/dispatch

## Claim: concurrent-acceptances-yield-one-assignment
Scope: component
Quantification: universal

The claim quantifies over "any number of drivers accepting concurrently". A test that accepts
twice sequentially satisfies the words and not the claim. Assignment is settled by a
compare-and-set against real storage, so the substituted version cannot fail the way production
can.

## Claim: late-acceptance-rejected
Scope: component

Rejection depends on reading committed assignment state. At unit scope this tests the handler's
branch rather than whether the state it branches on is real.

## Claim: expired-offer-withdrawn
Quantification: example
Residual: offer expiry is checked at a single boundary instant, not across the range of clock
skew between the driver client and the service.
Accepted: clock-skew handling is not yet designed. The claim as written does not cover it, and
inventing evidence for an undesigned mechanism would be worse than recording the gap.
