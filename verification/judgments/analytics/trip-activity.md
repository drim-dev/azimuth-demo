# Judgments: analytics/trip-activity

First pass, 2026-08-08. Two evidence defects were corrected before these verdicts: the e2e tag did
not originally assert the summary consequence it claimed, and redelivery varied each event's
multiplicity only from one to two under a `universal` tag. The corrected evidence is judged below.

## Claim: latest-version-is-projected
Verdict: sound
Fingerprint: 23d47ec72860a568
Judged: 2026-08-08
Judge: codex

The composed-stack test waits for all earlier trips and completions to reach Analytics, records the
summary, then creates and completes one trip through Trips, the outbox relay, RabbitMQ and Analytics.
It observes version four and `completed` through the per-trip endpoint, plus exactly one new total
and completed trip in the summary. Removing the outbox write, binding, relay or consumer makes the
bounded observation time out; incrementing a summary per delivery fails the exact delta. The e2e
tag is honestly `example`. The relay/backlog alerts are supplementary and their bindings resolve.

## Claim: redelivery-is-counted-once
Verdict: sound
Fingerprint: 4442f705b879c33b
Judged: 2026-08-08
Judge: codex

Across eight trials, each of four event ids is delivered between one and eight times in a shuffled
sequence through real RabbitMQ. The test waits until the four unique ids—not the delivery count—are
in the inbox, then asserts one projected trip and one summary contribution. Treating redelivery as a
new effect inflates either the inbox, projection or summary. `Universal` is accepted for the stated
multiplicity axis; the finite trial count is sampling of schedules, not a claim to enumerate them.

## Claim: older-delivery-is-inert
Verdict: sound
Fingerprint: 3cb2841f9b2a10a8
Judged: 2026-08-08
Judge: codex

The same real-broker trials shuffle versions and duplicates, deriving the expected state from the
maximum delivered version. A consumer that applies the last arrival rather than the greatest version
fails whenever an older event follows version four. The oracle is independent of the handler and no
expected final state is selected from arrival order. The aggregate-version comparison and inbox were
read directly; topology does not pretend to supply this consumer-specific rule.

## Claim: malformed-event-is-dead-lettered
Verdict: sound
Fingerprint: 3349c96739133e66
Judged: 2026-08-08
Judge: codex

The test publishes valid JSON carrying the impossible state `teleported`, followed by a valid event.
It observes the original bytes on Analytics' real dead-letter queue and the valid event in Postgres.
Accepting any non-empty state, requeueing the poison payload or stopping after rejection breaks a
distinct assertion. The `example` tag is accurate. `promtool` separately proves the dead-letter alert
fires; actual scrape and notification delivery remain recorded residue rather than claimed evidence.
