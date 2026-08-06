# Spec: trips/request

The rider's entry into the system: turning a valid quote into a trip awaiting dispatch.

Owns the admission rules for a ride request and the creation of the trip record. Does not own
what a quote means (`pricing/quote`), how drivers are found (`trips/dispatch`), or the trip's
subsequent states (`trips/lifecycle`).

## Requirement: valid-quote-required
Criticality: critical

A ride request SHALL be admitted only if it references a quote that exists and has not expired.

### Scenario: request-admitted-with-valid-quote
GIVEN a quote that has not expired
WHEN a rider submits a ride request referencing that quote
THEN the request is admitted

### Scenario: request-rejected-with-expired-quote
GIVEN a quote that has expired
WHEN a rider submits a ride request referencing that quote
THEN the request is rejected
AND no trip is created

### Scenario: request-rejected-with-unknown-quote
WHEN a rider submits a ride request referencing a quote identifier the system does not recognise
THEN the request is rejected
AND no trip is created

### Scenario: quote-consumed-once
GIVEN a quote that has already been consumed by an admitted ride request
WHEN a rider submits any further ride request referencing that quote
THEN the request is rejected
AND no second trip is created

## Requirement: trip-created
Criticality: standard

An admitted ride request SHALL create exactly one trip, in the requested state, carrying the
quoted fare.

### Scenario: trip-created-in-requested-state
WHEN a ride request is admitted
THEN exactly one trip is created
AND the trip is in the requested state
AND the trip carries the total from the referenced quote

### Scenario: rider-informed-of-trip
WHEN a trip is created
THEN the rider is given the trip identifier
AND the rider is shown that the trip is awaiting a driver

## Requirement: one-active-trip-per-rider
Criticality: critical

A rider SHALL NOT hold more than one trip that is not in a terminal state.

### Scenario: second-request-rejected-while-active
GIVEN a rider holding a trip that is not in a terminal state
WHEN that rider submits any further ride request
THEN the request is rejected
AND no second trip is created

### Scenario: request-admitted-after-terminal
GIVEN a rider whose only trip has reached a terminal state
WHEN that rider submits a ride request with a valid quote
THEN the request is admitted
