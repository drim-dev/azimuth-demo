# Spec: payments/capture

Turning a completed trip into exactly one charge against the rider.

Owns capture and its idempotency. Does not own fare computation (`pricing/quote`), driver payout
or the ledger's balance (`payments/ledger`, later).

## Requirement: capture-on-completion
Criticality: critical

A trip reaching the completed state SHALL result in a capture for its fare.

### Scenario: capture-created-on-completion
GIVEN a trip carrying a fare total
WHEN the trip reaches the completed state
THEN a capture is created for that total in that currency

### Scenario: no-capture-before-completion
GIVEN a trip that has not reached the completed state
WHEN the trip is examined
THEN no capture exists for it

### Scenario: no-capture-on-cancellation-without-fee
GIVEN a trip that reaches the cancelled state with no cancellation fee
WHEN the trip is examined
THEN no capture exists for it

## Requirement: captured-once
Criticality: critical

A trip SHALL have at most one capture, however many times completion is signalled, retried, or
processed concurrently.

### Scenario: duplicate-completion-event
GIVEN a trip that has been captured
WHEN a completion event for that trip is delivered any number of further times
THEN the trip still has exactly one capture

### Scenario: concurrent-completion-processing
GIVEN a trip that has not been captured
WHEN completion for that trip is processed concurrently by any number of workers
THEN exactly one capture is created

### Scenario: retry-after-transport-failure
GIVEN a capture attempt whose outcome was not observed by the caller
WHEN the caller retries the capture for the same trip
THEN the trip still has exactly one capture
AND the caller is told the original outcome

## Requirement: capture-amount-matches-quote
Criticality: critical

The captured amount SHALL equal the fare the trip carries, unless an adjustment with a recorded
reason applies.

### Scenario: capture-equals-trip-fare
GIVEN a trip carrying a fare total and no adjustment
WHEN the trip is captured
THEN the captured amount equals that total
AND the captured currency equals the trip's currency

### Scenario: adjusted-capture-records-reason
GIVEN a trip with an adjustment applied
WHEN the trip is captured
THEN the captured amount reflects the adjustment
AND the adjustment's reason is recorded with the capture

## Requirement: capture-failure-is-visible
Criticality: standard

A capture that cannot be completed SHALL leave the trip in a state that names the failure, and
SHALL NOT be silently dropped.

### Scenario: declined-capture-recorded
GIVEN a trip whose capture is declined by the payment provider
WHEN the trip is examined
THEN the trip records that capture was declined
AND the decline reason is recorded

### Scenario: declined-capture-is-retryable
GIVEN a trip whose capture was declined
WHEN capture is attempted again with a different instrument
THEN a capture may be created
AND the trip still has at most one capture
