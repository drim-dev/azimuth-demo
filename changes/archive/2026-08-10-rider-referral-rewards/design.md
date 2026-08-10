# Design: rider-referral-rewards

## Ownership and service boundary

Trips owns referral accounts, attribution and credit lifecycle because it already owns rider trip
admission and can decide whether a rider has crossed the first-trip boundary. Payments does not
learn rider identity or referral relationships. It owns capture, verifies the signed credit
authority carried by the trip lifecycle event, and emits the successful capture fact that may
qualify an attribution or confirm redemption.

The split deliberately forms a cycle of facts, not synchronous service calls: Trips authorizes a
credit on a trip event; Payments captures and publishes the result; Trips advances referral state.
The cycle is safe because each side commits an outbox or inbox with its local state and every
business transition has a storage uniqueness guard.

## Attribution and reward model

A referral account maps a rider identity to one generated id and stable encoded code. Account code
and rider identity are independently unique. An attribution is keyed by referred rider and names
the referrer; its insert occurs in the same admission transaction as the referred rider's first
trip. Existing trips and an existing attribution therefore close the eligibility window.

The first successful capture for an attributed rider qualifies that attribution. Two credit rows
are inserted with a unique `(attribution, beneficiary)` source key, one for each participant. That
constraint is the authority under redelivery and concurrent consumers; an event inbox is the fast
idempotency path, not the final guarantee. The initial fixed reward is 500 minor units in the
qualifying capture's currency and is explicit policy rather than a promotion abstraction.

## Reservation and payment authority

A ride request may name one public credit id. In the admission transaction Trips changes an owned
available credit to reserved by the new trip. An unknown, foreign, reserved, used, wrong-currency,
or over-fare credit rejects admission. Cancellation releases its reservation; a successful capture
changes it to used. The credit remains reserved after a decline so a changed payment instrument can
retry the same trip.

Trips signs a compact authority binding credit id, owner, trip id, amount and currency. The signed
authority is stored with the trip and repeated in lifecycle events. Payments verifies the
signature and all quote-bound values before treating the credit as a negative adjustment. A caller
cannot produce an adjusted capture merely by supplying an amount or reason. Payment stores original
fare, credit amount, referral credit id and captured amount for an auditable receipt.

## Capture publication

Payments writes one capture-event outbox row in the same transaction as a successful capture. A
confirmed publisher sends `payment.captured` to a durable exchange; Trips owns a referral queue and
dead-letter queue. The immutable envelope has a stable event id, trip id, original and captured
amounts, currency and optional credit id. It carries no rider identity.

Trips resolves rider and reserved credit from the trip. Its inbox records the capture event and
then, in the same transaction, confirms redemption and qualifies the rider's attribution when this
is their earliest successful capture. Per-source credit uniqueness prevents duplicate awards if
distinct event ids describe the same logical capture.

## Rider surface

The rider BFF forwards referral summary reads and the optional referral code and credit id on ride
admission. A referral panel exposes the stable code and named status. Available credits can be
selected for the next request; the receipt exposes original fare, referral credit and captured
total. The surface is intentionally functional rather than a campaign or social-sharing design.

## Work-package DAG

`contracts` precedes `trips-domain` and `payments-capture`. Those two packages can proceed in
parallel. `rider-surface` depends on the Trips HTTP contract but not Payments internals.
`broker-integration` joins Trips and Payments, after which `composed-evidence` can exercise the full
path. The coordinating agent owns shared contracts, intent/design artifacts, integration, Azimuth
judgments and archive state; package agents own only their assigned service or web trees and tests.

## Rejected alternatives and residue

A standalone Referral service was rejected for this fixture: it would need synchronous trip
eligibility queries or another complete rider/trip projection before demonstrating any additional
domain boundary. Applying a caller-provided payment adjustment was rejected because it makes the
money invariant depend on UI honesty. Issuing rewards at trip completion was rejected because a
declined payment would earn value.

The account key is the fixture's rider string and therefore does not model authenticated identity.
The fixed credit has no expiry, tax treatment or ledger posting. Broker declaration asserts desired
topology rather than externally deployed topology, and operational alert rules can observe lag and
dead letters without proving an operator will respond.
