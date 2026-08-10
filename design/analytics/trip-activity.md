# Design: analytics/trip-activity

## Requirement: trip-activity-reflects-lifecycle
Mechanism: transactional-event-relay
Enforcement: choke-point
Binding: dotnet-symbol:Trips.Features.Events.TripEventRelay.RelayPending
Mechanism: durable-broker-topology
Enforcement: constraint
Binding: dotnet-symbol:Common.Messaging.TripEventTopology.DeclareAsync
Mechanism: idempotent-activity-consumer
Enforcement: choke-point
Binding: dotnet-symbol:Analytics.Features.TripActivity.ConsumeTripStateChanged.RequestHandler.Handle

Trips writes a versioned lifecycle event in the same transaction as the trip state. The relay marks
the outbox row only after a confirmed publish; a crash between those operations produces a duplicate
rather than a lost transition. One durable topic exchange fans each event into separately owned
Payments and Analytics queues.

The aggregate version travels in the event because broker arrival order is not state order. Analytics
stores one row per trip and changes it only for a greater version. The operational summary groups
those rows rather than incrementing mutable counters, so duplicates and old versions need no
compensating arithmetic.

## Requirement: invalid-lifecycle-events-are-visible
Mechanism: dead-letter-topology
Enforcement: constraint
Binding: dotnet-symbol:Common.Messaging.TripEventTopology.DeclareAsync
Mechanism: invalid-event-dead-lettering
Enforcement: choke-point
Binding: dotnet-symbol:Analytics.Features.TripActivity.TripLifecycleConsumer.ExecuteAsync

The Analytics queue dead-letters rejected deliveries into its own durable queue. Invalid JSON or an
invalid envelope is rejected without requeue; a failure while applying a valid message is requeued.
Those outcomes differ because retry cannot make malformed bytes interpretable, while storage or
network failure may recover.

## Residue

The shared declaration establishes the topology applications request, not what an operator deployed
or what RabbitMQ permissions allow. Contract evolution has one schema version and no mixed-version
deployment. RabbitMQ queue metrics require its Prometheus plugin and scrape configuration; this
repository validates the alert expressions but does not deploy their scrape target or notification
route.
