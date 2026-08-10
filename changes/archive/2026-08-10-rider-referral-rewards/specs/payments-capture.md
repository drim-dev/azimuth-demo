# Intent delta: payments/capture

## Add requirement: successful-capture-is-published
Criticality: critical

A successful capture SHALL be published as an immutable fact after its payment record commits, and
publication retry SHALL NOT create another payment or another logical capture fact.

### Add scenario: committed-capture-is-published
GIVEN a successfully committed capture
WHEN the payment outbox is relayed
THEN a capture fact naming the trip, amount, currency and applied referral credit is published

### Add scenario: capture-publication-is-retryable
GIVEN publication whose confirmation was not retained
WHEN publication is retried
THEN consumers can identify it as the same capture fact
AND the trip still has exactly one payment capture
