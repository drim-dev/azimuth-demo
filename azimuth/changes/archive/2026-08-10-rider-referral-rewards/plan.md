# Plan: rider-referral-rewards

## WP0 — contracts and intent (coordinator; no dependencies)

- [x] Define proposal, intent deltas, architecture, verification obligations and work-package DAG.
- [x] Add signed referral authority and payment-captured broker contracts with codec tests.

## WP1 — Trips referral domain (Trips agent; depends on WP0 contracts)

- [x] Add referral account, immutable attribution, credit and capture-inbox persistence.
- [x] Add referral summary endpoint and admission-time code and credit rules.
- [x] Reserve, release and redeem credits transactionally and grant the reward pair idempotently.
- [x] Establish validator, storage-concurrency and Trips component evidence.

## WP2 — Payments capture publication (Payments agent; depends on WP0 contracts)

- [x] Verify referral authority and persist an auditable capture breakdown.
- [x] Write and relay capture facts transactionally with retry and dead-letter visibility.
- [x] Establish authority-mutation, outbox, redelivery and payment component evidence.

## WP3 — rider surface (Rider agent; depends on WP1 HTTP shape)

- [x] Add referral BFF routes, summary panel and optional code and credit request inputs.
- [x] Show original fare, credit and captured total on the receipt.
- [x] Establish accessible UI and browser evidence for named referral states.

## WP4 — integration (coordinator; depends on WP1, WP2 and WP3)

- [x] Wire programs, shared topology, migrations, composed environment and observability.
- [x] Establish composed-stack evidence for attribution, reward and later redemption.
- [x] Resolve integration defects without weakening the intent or evidence forms.

## WP5 — judgment and closure (coordinator; depends on WP4)

- [x] Run focused and full machine checks, then judge every standard and critical claim.
- [x] Record outcome, departures and framework validation measurements.
- [x] Apply the intent deltas, finalize the change and archive it.
