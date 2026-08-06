# Spec: trips/dispatch

Matching a requested trip to a driver: which drivers are offered the trip, and how exactly one
of them comes to hold it.

Owns offers and acceptance. Does not own driver eligibility rules beyond availability
(`driver/availability`, later), nor the trip's states after assignment (`trips/lifecycle`).

## Requirement: offer-to-available-drivers
Criticality: standard

A requested trip SHALL be offered to available drivers near its pickup, and to no others.

### Scenario: offer-sent-to-available-nearby-driver
GIVEN a driver who is available and near the pickup
WHEN a trip enters the requested state
THEN that driver receives an offer for the trip

### Scenario: unavailable-driver-not-offered
GIVEN a driver who is not available
WHEN a trip enters the requested state
THEN that driver receives no offer

### Scenario: no-available-drivers
GIVEN no available drivers near the pickup
WHEN a trip enters the requested state
THEN no offer is made
AND the rider is told no driver is available

## Requirement: single-acceptance
Criticality: critical

At most one driver SHALL hold an accepted offer for a trip, however many drivers accept and
however close together in time.

### Scenario: first-acceptance-assigns
GIVEN a trip offered to several drivers
WHEN one driver accepts the offer
THEN that driver is assigned to the trip
AND the trip leaves the requested state

### Scenario: concurrent-acceptances-yield-one-assignment
GIVEN a trip offered to several drivers
WHEN any number of those drivers accept concurrently
THEN exactly one of them is assigned to the trip
AND every other accepting driver is told the offer was taken

### Scenario: late-acceptance-rejected
GIVEN a trip that already has an assigned driver
WHEN any further driver accepts an offer for that trip
THEN the acceptance is rejected
AND the assignment is unchanged

## Requirement: offers-withdrawn-on-assignment
Criticality: standard

Once a trip is assigned, outstanding offers for it SHALL be withdrawn.

### Scenario: other-offers-withdrawn
GIVEN a trip offered to several drivers
WHEN one driver is assigned to the trip
THEN every other driver's offer for that trip is withdrawn

### Scenario: expired-offer-withdrawn
GIVEN an offer that has reached its expiry without acceptance
WHEN the offer is examined
THEN it is withdrawn
AND the driver is no longer shown the trip
