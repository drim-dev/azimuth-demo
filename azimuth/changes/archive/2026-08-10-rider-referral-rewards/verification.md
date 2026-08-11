# Verification: rider-referral-rewards

## Claim: attribution-is-single#known-code-is-attributed
Scope: component
Quantification: example
Oracle: direct

Admit through HTTP against Postgres and read the referral summary. The evidence must not seed the
attribution that it claims admission creates.

## Claim: attribution-is-single#attribution-cannot-be-replaced
Scope: component
Quantification: universal
Oracle: relational

Vary whether eligibility was closed by an attribution, an unattributed admitted trip, or competing
requests. In every ordering, at most one attribution survives and later codes do not alter it.

## Claim: reward-follows-first-capture#first-capture-awards-pair
Scope: e2e
Quantification: example
Oracle: direct

Cross quote, trip admission, completion, lifecycle broker, capture, payment outbox and referral
consumer. Observe both participants through the public summary; a direct handler call cannot cover
the qualification path.

## Claim: reward-follows-first-capture#capture-redelivery-does-not-duplicate-reward
Scope: component
Quantification: universal
Oracle: relational

Deliver duplicates and distinct event ids for the same capture, sequentially and concurrently.
The relation between delivery multiplicity and the two source-keyed credits remains constant.

## Claim: credit-redemption-is-authorized-once#owned-credit-reduces-capture
Scope: e2e
Quantification: example
Oracle: relational

The signed quote gives original fare, the referral summary gives credit value, and the receipt must
satisfy `captured = original - credit` while naming the same credit as used.

## Claim: credit-redemption-is-authorized-once#forged-credit-authority-is-rejected
Scope: component
Quantification: universal
Oracle: metamorphic

Mutate each authority binding in turn while leaving a valid quote fixed. Every mutation must lose
adjustment authority and leave a visible failure; this cannot be evidenced by token round-trip only.

## Claim: successful-capture-is-published#capture-publication-is-retryable
Scope: component
Quantification: universal
Oracle: relational

Vary relay confirmation retention and delivery count through real RabbitMQ. Published envelopes
retain one event id and the database retains one capture.

## Claim: rider-sees-referral-state#referral-summary-explains-state
Scope: e2e
Quantification: example
Oracle: direct

Use the rendered rider page and semantic accessible names to establish that code, named status and
credit state are visible without color.

## Planned deviations

The composed journey may use an accelerated test-only trip transition path already present in the
fixture. Provider settlement remains a deterministic fixture boundary; component evidence verifies
decline and retry independently rather than calling an external processor.
