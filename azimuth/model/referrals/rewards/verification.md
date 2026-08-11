# Verification: referrals/rewards

## Claim: known-code-is-attributed
Scope: component
Quantification: example
Oracle: direct

Admission crosses HTTP and Postgres, then the public summary and stored attribution independently
name the code owner. Unknown and self-code examples additionally establish that refusal rolls back
the permanent admission marker.

## Claim: attribution-cannot-be-replaced
Scope: component
Quantification: universal
Oracle: relational

Evidence closes eligibility by an attribution, an unattributed admitted trip, and eight contending
first requests. Each relation leaves one permanent admission and at most one attribution.

## Claim: no-reward-before-capture
Scope: component
Quantification: universal
Oracle: direct

Every trip lifecycle state is traversed without a payment-capture fact and leaves both participants
without source credits. The composed journey repeats the check after admission and before capture.

## Claim: first-capture-awards-pair
Scope: e2e
Quantification: example
Oracle: direct
Residual: one composed currency and participant pair are sampled; the duplicate-delivery scenario
ranges the concurrency axis separately
Accepted: the material uncertainty is whether the asynchronous process composition awards both
participants at all; repeating fixture identities or the same fixed policy value would not widen it

The composed test crosses quote, admission, trip lifecycle outbox, real RabbitMQ, Payments capture,
payment outbox, referral consumer and both public summaries. Handler-level evidence separately
isolates the exact pair insertion.

## Claim: capture-redelivery-does-not-duplicate-reward
Scope: component
Quantification: universal
Oracle: relational

The broker delivers the same capture repeatedly, then concurrent handlers process distinct event
ids for the same logical capture. Delivery multiplicity changes while the source-keyed pair remains
constant.

## Claim: owned-credit-reduces-capture
Scope: component
Quantification: universal
Oracle: relational

The composed receipt satisfies `captured = original - credit` and names the same credit later shown
as used. Component evidence ranges credit and fare over three currencies and checks provider,
capture, status and outbox values against the same relation.

## Claim: unavailable-credit-is-rejected
Scope: component
Quantification: universal
Oracle: direct

Evidence ranges unknown public ids and every stored state, plus foreign ownership. Each refusal is
observed through HTTP and leaves no admitted trip or changed reservation.

## Claim: forged-credit-authority-is-rejected
Scope: component
Quantification: universal
Oracle: metamorphic

Starting from valid authority, evidence mutates the body, signer, trip, currency and amount. Every
transformation prevents a provider call and leaves the stable quarantine reason visible.

## Claim: capture-redelivery-does-not-redeem-twice
Scope: component
Quantification: universal
Oracle: relational

Concurrent settlement workers retain one capture and payment event. Trips separately processes
distinct capture-event ids concurrently and leaves the reserved credit used by exactly one capture.

## Claim: referral-summary-explains-state
Scope: e2e
Quantification: example
Oracle: direct

The real-process journey reads the public summary through the BFF and then opens the production
server-rendered referral page. It observes code, qualification, exact credit amount/currency and the
textual `used` state; CSS color is not its oracle.

## Residual: authenticated-rider-identity
Accepted: the demo labels its session-stable id as unauthenticated; replace it before treating codes
or balances as account-confidential data

The behavior and concurrency claims operate over stable rider keys. They do not establish who may
claim a key, because authentication is outside this fixture.
