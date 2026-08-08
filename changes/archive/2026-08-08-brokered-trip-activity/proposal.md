# Change: brokered-trip-activity

Status: accepted and complete

## Problem

The fixture has asynchronous settlement but no message broker. Its database outbox exercises
transactional handoff and retry, while leaving the framework's explicit broker question untouched:
producer and consumer tags can both be honest even when topology routes no message between them.

## Scope

Publish committed, versioned trip lifecycle events through a real broker. Payments consumes
completion events into its local settlement inbox; Analytics consumes every lifecycle event into an
operational projection. Both consumers tolerate redelivery and out-of-order arrival independently.

Add standard requirements `trip-activity-reflects-lifecycle` and
`invalid-lifecycle-events-are-visible` under `analytics/trip-activity`. Re-establish the existing
critical payment claims whose handoff moves from a shared table to the broker.

## Completion

- the trip transition and its outbox event commit atomically;
- a relay publishes pending events and retries an unconfirmed publish;
- Payments and Analytics own separate durable queues and inbox state;
- duplicate and older deliveries cannot duplicate capture or rewind analytics;
- malformed messages leave the active queue and appear in a dead-letter queue;
- broker topology, lag and dead-letter mechanisms are named in design and evidence;
- component evidence uses a real broker, and composed-stack evidence crosses the relay and both
  consumers.
