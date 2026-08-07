# Judgments: payments/capture

Re-judged 2026-08-08 after design bindings, migration metadata and bound source entered the
freshness fingerprint. The unique index properties, transactional intent writer and capture
constructor were read directly; the existing verdict rationales remain applicable.

Re-judged for `market-aware-surge-quotes`. The first pass exposed and corrected two false evidence
links: seeded intents were not evidence of completion, and a cancellation helper that did nothing
was not evidence of cancellation. Both claims now run through real Trips and Payments processes.

## Claim: capture-created-on-completion
Verdict: sound
Fingerprint: 7abb1c587d02a93f
Judged: 2026-08-08
Judge: codex

The e2e moves a real trip through accept, start and complete, then dispatches Payments and reads the
capture. `TransitionTrip` was checked against the source: it inserts the quote token into
`capture_intents` before committing the same database transaction. Removing either the outbox write
or dispatcher makes the final lookup fail. The tag is honestly `example`, with the weakening from
the universal floor accepted in the verification plan.

## Claim: no-capture-before-completion
Verdict: sound
Fingerprint: faeb693275cf9723
Judged: 2026-08-08
Judge: codex

The same real-process test dispatches Payments immediately after admission and observes 404 for that
trip before allowing any lifecycle transition. A premature intent or a dispatcher that captures
all trips fails. The one-state sample is recorded rather than disguised as universal.

## Claim: no-capture-on-cancellation-without-fee
Verdict: sound
Fingerprint: 736188b9322a67d4
Judged: 2026-08-08
Judge: codex

The e2e now actually cancels through Trips, dispatches Payments and observes no capture. This
replaces the component test whose `CancelWithoutFee` helper performed no action. Writing an intent
on the no-fee cancellation branch fails the new evidence.

## Claim: duplicate-completion-event
Verdict: sound
Fingerprint: e18e4d13be885bec
Judged: 2026-08-08
Judge: codex

The Trips component test completes a real stored trip, sends six concurrent completion replays for
five trials, and asserts one completion history entry and one capture intent. It fails if a replay
writes another outbox row even when Payments itself remains idempotent.

## Claim: concurrent-completion-processing
Verdict: sound
Fingerprint: 6f3a54d2d20e68df
Judged: 2026-08-08
Judge: codex

Eight workers dispatch one pending intent concurrently against real PostgreSQL for five trials; all
requests complete and exactly one live capture exists. Dropping `ux_capture_trip` permits more than
one row and fails the count. Distinct workers, rather than sequential repeats, exercise the race.

## Claim: retry-after-transport-failure
Verdict: sound
Fingerprint: 45326ba006eb729f
Judged: 2026-08-08
Judge: codex

The provider seam returns `Unobserved` followed by `Captured`, five dispatch retries are made, and
the real store contains one capture. Treating `Unobserved` as a safe failure causes a second provider
attempt and can violate the unique path; removing the persistence on the unobserved branch leaves
the asserted count at zero. Provider reconciliation remains a design residue.

## Claim: capture-equals-trip-fare
Verdict: sound
Fingerprint: 95de6075022919d7
Judged: 2026-08-08
Judge: codex

Component evidence ranges over amounts and currencies encoded as multi-component signed quotes,
asserts the stored capture, and proves an altered token reaches neither provider nor table. The e2e
carries a non-zero surge from Pricing through the trip outbox and asserts the capture equals the
rider-visible total. Trusting a separate intent amount or omitting surge fails; neither field exists
on the intent anymore. `CaptureTrip` was checked and decodes before provider I/O.

## Claim: adjusted-capture-records-reason
Verdict: sound
Fingerprint: 51db3d46466b64fd
Judged: 2026-08-08
Judge: codex

For four reasons and 24 generated cases, the test keeps the signed quote total separate from a
non-zero positive or negative adjustment, then asserts both the changed captured amount and exact
reason. Ignoring the adjustment now fails the amount inequality; ignoring the reason fails the
record assertion. The handler rejects an unreasoned or negative-result adjustment.

## Claim: declined-capture-recorded
Verdict: sound
Fingerprint: e564379eed313b2d
Judged: 2026-08-08
Judge: codex

A scripted provider decline produces no capture and a persisted `declined` failure. Silently
dropping the outcome or treating it as success fails one of the two assertions. The `example` tag
matches the one decline reason.

## Claim: declined-capture-is-retryable
Verdict: sound
Fingerprint: a3eeb84068dd6d32
Judged: 2026-08-08
Judge: codex

The provider declines once and succeeds on the next dispatch. Evidence asserts absence after the
first, presence after the second, one capture total and one recorded failure. Marking a declined
intent dispatched or allowing duplicate captures fails. The tag is `example`, as required for this
standard claim.
