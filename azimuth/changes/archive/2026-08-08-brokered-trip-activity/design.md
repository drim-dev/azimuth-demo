# Design: brokered-trip-activity

## Transaction and publication

Trips writes a `trip_events` outbox row in the same Postgres transaction as each created or changed
trip state. A relay publishes the immutable event to a durable topic exchange and marks the row only
after broker confirmation. A crash between confirmation and the mark produces redelivery, not loss.

The event carries an event id, trip id, monotonically increasing trip version, current state,
occurrence instant and the signed quote token needed by Payments. It does not carry rider or driver
identity. The version belongs to the aggregate, not the broker, because partition and replay can
invalidate broker delivery order.

## Topology as mechanism

One durable exchange fans the same event to independently owned Payments and Analytics queues.
Queue bindings are part of the mechanism: a producer and consumer with no matching binding do not
realize the feature. The first implementation declares topology from a shared contract because the
fixture has one deployment unit; this is an asserted topology, not broker discovery.

Each active queue has its own dead-letter exchange and durable dead-letter queue. Consumers reject
unparseable messages without requeue. Transient infrastructure failures leave deliveries unacked so
the broker may redeliver them.

## Consumer-specific discharge

Payments stores the event id and the highest trip version in its own transaction before creating a
capture intent. Only a newer `completed` state creates settlement work. Its existing unique capture
constraint remains the last line against duplicate charge.

Analytics stores one row per trip containing the highest applied version and current state. Its
summary groups those rows at query time, so redelivery cannot increment a counter twice. An event
with a version at or below the stored version records no state change.

## Rejected alternatives

Replacing the capture table with a broker queue alone was rejected: without a producer outbox, a
committed completion can be lost between Postgres and RabbitMQ. Trusting queue order was rejected:
redelivery and multiple queues make order a consumer concern. Maintaining mutable aggregate counters
was rejected because compensating an out-of-order state replacement is unnecessary complexity for
the fixture.

## Residue

The shared topology declaration proves what the applications request from RabbitMQ, not what an
operator deployed or what permissions allow. Broker confirmations narrow loss but do not prove an
external managed service retained the message. Contract evolution has one schema version and no
mixed-version deployment yet; the first incompatible event must force that question rather than add
unused compatibility machinery now.
