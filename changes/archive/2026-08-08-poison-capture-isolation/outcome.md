# Outcome: poison-capture-isolation

Status: accepted

## Result

Settlement now orders pending work deterministically, quarantines invalid signed quotes atomically
with a terminal failure, and continues with independent intents. Unexpected per-intent failures are
deferred rather than terminating the batch; cancellation still propagates immediately.

## Departures

The dispatch endpoint now returns captured, quarantined and deferred counts instead of surfacing an
invalid internal outbox row as HTTP 422. That is a deliberate contract correction: the caller asks
to drain a batch, not to validate the trip service's stored payload.

## Residual decisions

Quarantine is represented by `DispatchedAt` plus a capture failure rather than a separate dead-letter
table. There is no re-drive operation for an invalid signed quote; recovery requires correcting the
source data under an operational procedure that this fixture does not yet model.

Only invalid signed quotes are classified as terminal. Unknown failures stay pending and remain
visible through overdue metrics, which avoids discarding a potentially transient provider fault.

## Measurements

The accepted model contains 64 claims and no holes. Payments has 18 passing component tests. The
new claim's example uses one malformed intent followed by two valid intents and a second settlement
cycle; the agent tier judged it sound.

The intended field measurement for site-scoped freshness was confounded by the immediately prior
receipt-schema change, which legitimately changed every fingerprint identity. Sixty-one existing
verdict fingerprints were migrated once after confirming their evidence and verdict text were
unchanged. Isolation will be measured against the next product evidence edit from this clean
baseline rather than presenting this run as evidence it cannot supply.
