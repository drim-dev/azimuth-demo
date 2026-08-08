# Verification: brokered-trip-activity

## Claim: latest-version-is-projected
Scope: e2e
Quantification: example
Oracle: direct

The composition must cross Trips' transaction, relay, real broker, Analytics consumer and its own
store. Component evidence separately drives reordered versions directly through a real queue.

## Claim: redelivery-is-counted-once
Scope: component
Quantification: universal
Oracle: direct

Evidence varies delivery count and checks both the per-trip row and derived summary against real
RabbitMQ and Postgres. The event id and aggregate version are separate axes; the test varies both so
one guard cannot falsely establish the other.

## Claim: older-delivery-is-inert
Scope: component
Quantification: universal
Oracle: model-based

Generate permutations and duplicates of a versioned state history. The expected projection is the
maximum version from the generated delivery sequence, independent of arrival order.

## Claim: malformed-event-is-dead-lettered
Scope: component
Quantification: example
Oracle: direct

Publish malformed bytes followed by a valid envelope. Assert the malformed delivery appears on the
real dead-letter queue and the valid event appears in the projection. The claim is not satisfied by
an in-memory handler test because queue acknowledgement is its subject.

## Existing claim: payments/capture#capture-created-on-completion

Re-establish at e2e scope without a manual dispatch call. The test must observe that the trip outbox
is relayed and the Payments queue produces a capture. Existing component evidence remains the amount
oracle from the local capture intent onward.

## Existing claim: payments/capture#duplicate-completion-event

Add broker-backed component evidence that redelivers the same completion and a distinct older event.
The existing capture uniqueness evidence remains required; the inbox does not replace it.
