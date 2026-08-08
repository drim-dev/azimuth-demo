# Verification: analytics/trip-activity

## Claim: latest-version-is-projected
Scope: e2e
Quantification: example
Oracle: direct
Strength: detection
Evidence: outbox age and the Analytics queue backlog feed repository-owned Prometheus rules
Binding: prometheus-alert:TripEventRelayStalled, prometheus-alert:TripEventAnalyticsBacklog
Re-established: continuously
Dies silently: Trips is not scraped, RabbitMQ metrics are absent, either rule is absent, or
notification routing is muted
Detector test: the component metric test injects a fresh then overdue outbox event; promtool drives
both alert expressions with synthetic time series
Detector binding: dotnet-symbol:Trips.Tests.Features.Events.TripEventMetricsTests.Metrics_expose_fresh_and_overdue_unpublished_events, prometheus-rule-test:TripEventRelayStalled, prometheus-rule-test:TripEventAnalyticsBacklog
Residual: the e2e composition samples one completed history rather than every terminal state
Accepted: component evidence separately ranges over reordered states; e2e establishes the real
producer, relay, broker, consumer and store composition

## Claim: redelivery-is-counted-once
Scope: component
Quantification: universal
Oracle: direct

The real-broker test varies delivery count and checks both the per-trip row and derived summary.
It reuses event ids for redelivery rather than merely sending similar payloads.

## Claim: older-delivery-is-inert
Scope: component
Quantification: universal
Oracle: model-based

The test generates reordered and duplicated version histories. Its expected result is derived as the
maximum delivered version, independent of the order generated for each trial.

## Claim: malformed-event-is-dead-lettered
Scope: component
Quantification: example
Oracle: direct
Strength: detection
Evidence: the Analytics and Payments dead-letter queues feed a repository-owned Prometheus rule
Binding: prometheus-alert:TripEventDeadLetters
Re-established: continuously
Dies silently: RabbitMQ metrics are not scraped, the rule is absent, or notification routing is muted
Detector test: the component test publishes malformed bytes and observes them on the real dead-letter
queue; promtool proves the alert fires for a non-empty dead-letter series
Detector binding: dotnet-symbol:Analytics.Tests.Features.TripActivity.TripActivityProjectionTests.Malformed_delivery_is_dead_lettered_without_blocking_the_valid_one, prometheus-rule-test:TripEventDeadLetters

The same component test publishes a valid event behind the poison message and observes its projection,
so dead-lettering cannot pass by stopping the consumer.
