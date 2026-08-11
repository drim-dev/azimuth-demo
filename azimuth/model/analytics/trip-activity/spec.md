# Spec: analytics/trip-activity

An operational projection of trip lifecycle state, delivered independently of the transaction that
owns the trip.

Owns eventual trip activity and delivery diagnosis. Does not own the trip state machine
(`trips/lifecycle`), payment settlement (`payments/capture`) or business reporting definitions.

## Requirement: trip-activity-reflects-lifecycle
Criticality: standard

The operational trip activity projection SHALL eventually reflect each committed trip state
version once and SHALL NOT be rewound by an older delivery.

### Scenario: latest-version-is-projected
GIVEN a trip with committed lifecycle transitions
WHEN their events are delivered
THEN the projection names the trip's latest state and version
AND the operational summary includes that trip once

### Scenario: redelivery-is-counted-once
GIVEN a lifecycle event already applied to the projection
WHEN that event is delivered any number of further times
THEN the projection and summary remain unchanged

### Scenario: older-delivery-is-inert
GIVEN the projection has applied a trip state version
WHEN any older version is delivered afterward
THEN the projection is not rewound

## Requirement: invalid-lifecycle-events-are-visible
Criticality: standard

A lifecycle message that cannot be interpreted SHALL leave the active delivery queue and SHALL
remain visible for diagnosis.

### Scenario: malformed-event-is-dead-lettered
GIVEN a malformed lifecycle message precedes a valid message
WHEN the analytics consumer receives both
THEN the malformed message is placed in the analytics dead-letter queue
AND the valid message is still projected
