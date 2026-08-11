# Intent delta: analytics/trip-activity

## Add requirement: trip-activity-reflects-lifecycle
Criticality: standard

The operational trip activity projection SHALL eventually reflect each committed trip state
version once and SHALL NOT be rewound by an older delivery.

### Add scenario: latest-version-is-projected
GIVEN a trip with committed lifecycle transitions
WHEN their events are delivered
THEN the projection names the trip's latest state and version
AND the operational summary includes that trip once

### Add scenario: redelivery-is-counted-once
GIVEN a lifecycle event already applied to the projection
WHEN that event is delivered any number of further times
THEN the projection and summary remain unchanged

### Add scenario: older-delivery-is-inert
GIVEN the projection has applied a trip state version
WHEN any older version is delivered afterward
THEN the projection is not rewound

## Add requirement: invalid-lifecycle-events-are-visible
Criticality: standard

A lifecycle message that cannot be interpreted SHALL leave the active delivery queue and SHALL
remain visible for diagnosis.

### Add scenario: malformed-event-is-dead-lettered
GIVEN a malformed lifecycle message precedes a valid message
WHEN the analytics consumer receives both
THEN the malformed message is placed in the analytics dead-letter queue
AND the valid message is still projected
