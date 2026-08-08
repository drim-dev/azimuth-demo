# Plan: brokered-trip-activity

- [x] Add the accepted intent delta and proposed mechanism.
- [x] Add shared event contract and declared RabbitMQ topology.
- [x] Write versioned outbox events with trip state transactions and relay with confirmation.
- [x] Move Payments' completion handoff behind its durable queue and local inbox.
- [x] Add Analytics projection storage, consumer and operational summary endpoint.
- [x] Add dead-letter behavior and settlement/projection observability.
- [x] Establish component and composed-stack evidence through real RabbitMQ and Postgres.
- [x] Run the agent tier, record departures and measurements, then finalize and archive.
