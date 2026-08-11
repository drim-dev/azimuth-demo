# Outcome: brokered-trip-activity

Status: accepted

## Departures

- The shared event contract gained semantic validation for state, quote token and payment method
  after syntactically valid but meaningless envelopes exposed the gap.
- Consumers gained a reconnect loop for broker startup and channel failure after TCP readiness was
  shown not to imply AMQP readiness.
- Composed-stack setup probes RabbitMQ's TCP port directly instead of depending on a CLI readiness
  command. This is harness behavior, not a product mechanism.
- Analytics performs a translatable database projection and maps the result to its response DTO in
  memory; the initially proposed record projection could not be translated by EF/Postgres.

## Residual decisions

- Shared declarations establish requested topology, not what an independently managed environment
  deployed. A deployment-side topology enumerator remains to be designed.
- The event has one schema version. Mixed-version deployment and incompatible evolution are not yet
  exercised.
- Consumer reconnect is exercised by startup timing, but there is no controlled live broker-restart
  chaos test.
- Prometheus rules are evaluated, but no real RabbitMQ scrape or Alertmanager notification delivery
  is tested.

## Measurements

- The change added two standard requirements containing four claims.
- The repository run contains 204 tests: 93 core, 38 extractor, 63 service/component and 10 e2e,
  plus five Prometheus rule-test cases. Analytics contributes two real RabbitMQ/Postgres component
  tests; Payments adds broker-backed completion evidence; composed-stack evidence crosses Trips,
  RabbitMQ, Payments and Analytics.
- Seven material findings changed code or evidence: an untranslatable summary query, incomplete
  event validation, missing broker reconnect, extractor dependency-resolution failure, missing
  summary consequence in e2e evidence, overdeclared redelivery multiplicity, and a repository check
  that compiled but did not execute two TypeScript extractor test files.
- The agent tier re-read 25 stale judgments and added four current sound judgments. Shared design
  and composed-stack changes caused much of the conservative invalidation.
- Both extractors fingerprint all 83 emitted covering sites.
- Authoring minutes were not measured; this run does not settle ceremony cost.
