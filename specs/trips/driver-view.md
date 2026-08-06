# Spec: trips/driver-view

What a driver is shown about an offer and a trip they hold.

The asymmetry with `trips/rider-view` is the point: a driver sees the rider's pickup before
accepting and their contact only after, which is the mirror image of the rider's rules and
therefore the case where a shared projection would be wrong.

Owns driver-facing visibility. Does not own dispatch itself (`trips/dispatch`).

## Invariant: rider-contact-confined-to-held-trips
Criticality: critical
Over: trips/driver-view

No site that carries trip information to a driver SHALL expose a rider's contact details for a trip
that driver does not currently hold.

Ranges over a set of sites, not over executions. Its class is every site realizing a claim in this
spec, and membership is derived from what the code built — the same shape as
`trips/rider-view#position-confined-to-live-phases`, which was written after a second surface leaked
past the behavioural claims.

## Requirement: offer-shows-pickup-not-rider
Criticality: critical

Before accepting, a driver SHALL be shown the pickup area and the fare, and no identifying detail
of the rider.

### Scenario: pickup-shown-on-offer
GIVEN an offer for a trip
WHEN the driver views the offer
THEN the pickup area and the fare are shown

### Scenario: rider-contact-hidden-on-offer
GIVEN an offer for a trip the driver has not accepted
WHEN the driver views the offer
THEN no rider contact detail is shown

## Requirement: held-trip-shows-rider-contact
Criticality: standard

While a driver holds a trip that is not in a terminal state, they SHALL be shown a proxy contact
for the rider.

### Scenario: proxy-contact-while-held
GIVEN a trip the driver holds, in the assigned or in-progress state
WHEN the driver views the trip
THEN a proxy contact for the rider is shown

### Scenario: contact-withdrawn-after-terminal
GIVEN a trip the driver held that has reached a terminal state
WHEN the driver views the trip
THEN no rider contact is shown
