# Design: trips/driver-view

## Requirement: offer-shows-pickup-not-rider
Mechanism: offer-projection
Enforcement: guard
Binding: dotnet-symbol:Trips.Domain.DriverProjection.Offer

The mirror of `RiderProjection`, and deliberately a second type rather than a shared one. The two
sides have opposite rules — the rider sees the driver only *after* assignment, the driver sees the
rider's contact only *while holding* — and a shared projection with a direction flag is one
conditional away from returning the wrong side's data.

## Requirement: rider-contact-confined-to-held-trips
Mechanism: driver-trip-projection
Enforcement: guard
Binding: dotnet-symbol:Trips.Domain.DriverProjection.For

Written as an invariant from the start rather than after a leak, because the rider side already
paid for that lesson.

## Residue

**Proxy contacts are not implemented.** The projection returns a placeholder token where a real
system would mint a per-trip proxy number that expires at trip end. Concern C2 in the catalog. The
shape is right and the mechanism is absent, which is the honest state — the claim says "a proxy
contact", and a placeholder satisfies it while a rider's real number never enters the model.
