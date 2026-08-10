# Judgments: payments/capture

Revalidated 2026-08-10 after D29 separated relational, metamorphic and model-based oracles. Every
stale claim, covering body, realization site and design binding was re-read. The behavioral sources
are unchanged; the capture-amount test now honestly names its within-case fare/capture relation as
`relational`. Altered-token refusal remains a separate contract oracle.

Re-judged 2026-08-10 after D28 exposed realization sources. The event consumer owns the
completion-only intent and its deduplication; dispatch and capture handlers own batch progress,
provider outcomes, amount derivation and database idempotency; query/update handlers and the rider
receipt path own their stated observations. The trip transition, broker topology and settlement
metrics remain applicable mechanism or detector inputs, but no longer claim to realize payment
predicates they do not enforce.

Revalidated 2026-08-10 after D27 added stable mechanism ids. The semantic diff changed no
enforcement, binding, expectation, rationale, claim, evidence form or source; the prior verdict
rationales therefore remain applicable and only their freshness fingerprints moved.

Re-judged 2026-08-08 after completion crossed a real broker. The producer transaction, declared
topology, Payments inbox and automatic settlement were read as separate mechanisms. Broker
redelivery evidence replaced the former shared-table assertion for `duplicate-completion-event`;
unchanged capture tests were re-read because the payment design file correctly expired them.

Rebased 2026-08-08 after criticality entered claim freshness and the shared standard recorded the
first detector chain. No level changed. The payment detector was re-read through its two alert-rule
bindings, component metric test and two `promtool` rule-test bindings; all resolved.

Re-judged 2026-08-08 after design bindings, migration metadata and bound source entered the
freshness fingerprint. The unique index properties, transactional intent writer and capture
constructor were read directly; the existing verdict rationales remain applicable.

Re-judged for `market-aware-surge-quotes`. The first pass exposed and corrected two false evidence
links: seeded intents were not evidence of completion, and a cancellation helper that did nothing
was not evidence of cancellation. Both claims now run through real Trips and Payments processes.

## Claim: capture-created-on-completion
Verdict: sound
Fingerprint: 44343c7bbf5a2693
Judged: 2026-08-10
Judge: codex

The e2e moves a real trip through accept, start and complete, then waits without calling the dispatch
endpoint. Trips commits a versioned event with the state, the relay publishes it through the Payments
binding, the inbox creates local settlement work, and the worker creates the capture. Removing any
handoff makes the final lookup fail. The tag is honestly `example`, with the weakening from the
universal floor accepted in the verification plan.

## Claim: no-capture-before-completion
Verdict: sound
Fingerprint: fc98abf973a4443a
Judged: 2026-08-10
Judge: codex

The same real-process test dispatches Payments immediately after admission and observes 404 for that
trip before allowing any lifecycle transition. A premature intent or a dispatcher that captures
all trips fails. The one-state sample is recorded rather than disguised as universal.

## Claim: no-capture-on-cancellation-without-fee
Verdict: sound
Fingerprint: fb2cca0773fcc6ba
Judged: 2026-08-10
Judge: codex

The e2e now actually cancels through Trips, dispatches Payments and observes no capture. This
replaces the component test whose `CancelWithoutFee` helper performed no action. Writing an intent
on the no-fee cancellation branch fails the new evidence.

## Claim: duplicate-completion-event
Verdict: sound
Fingerprint: ee3aa5985150d45c
Judged: 2026-08-10
Judge: codex

The Payments component test sends one completion event seven times through real RabbitMQ, followed
by a distinct older event. It observes two unique inbox entries, a version-four cursor and one local
settlement intent. Reprocessing by delivery rather than event id, or accepting the older version,
breaks a distinct assertion. The capture constraint remains separate evidence for the final effect.

## Claim: concurrent-completion-processing
Verdict: sound
Fingerprint: b9520d9af9c874f3
Judged: 2026-08-10
Judge: codex

Eight workers dispatch one pending intent concurrently against real PostgreSQL for five trials; all
requests complete and exactly one live capture exists. Dropping `ux_capture_trip` permits more than
one row and fails the count. Distinct workers, rather than sequential repeats, exercise the race.

## Claim: retry-after-transport-failure
Verdict: sound
Fingerprint: 7de31cd88f41561f
Judged: 2026-08-10
Judge: codex

The provider seam returns `Unobserved` followed by `Captured`, five dispatch retries are made, and
the real store contains one capture. Treating `Unobserved` as a safe failure causes a second provider
attempt and can violate the unique path; removing the persistence on the unobserved branch leaves
the asserted count at zero. Provider reconciliation remains a design residue.

## Claim: capture-equals-trip-fare
Verdict: sound
Fingerprint: 167ec16be98aef17
Judged: 2026-08-10
Judge: codex

Component evidence ranges over amounts and currencies encoded as multi-component signed quotes and
relates each admitted fare to the stored capture from the same settlement case. Separate contract
evidence proves an altered token reaches neither provider nor table. The e2e carries a non-zero
surge from Pricing through the trip outbox and asserts the capture equals the rider-visible total.
Trusting a separate intent amount or omitting surge fails; neither field exists on the intent
anymore. `CaptureTrip` was checked and decodes before provider I/O.

## Claim: adjusted-capture-records-reason
Verdict: sound
Fingerprint: 114a76d4572214fa
Judged: 2026-08-10
Judge: codex

For four reasons and 24 generated cases, the test keeps the signed quote total separate from a
non-zero positive or negative adjustment, then asserts both the changed captured amount and exact
reason. Ignoring the adjustment now fails the amount inequality; ignoring the reason fails the
record assertion. The handler rejects an unreasoned or negative-result adjustment.

## Claim: declined-capture-recorded
Verdict: sound
Fingerprint: 9aa586accba8e1e4
Judged: 2026-08-08
Judge: codex

A scripted provider decline produces no capture and a persisted `declined` failure. Silently
dropping the outcome or treating it as success fails one of the two assertions. The `example` tag
matches the one decline reason.

## Claim: declined-capture-is-retryable
Verdict: sound
Fingerprint: 1ffc9ae5531b7db7
Judged: 2026-08-08
Judge: codex

The provider declines `default`, the application refuses another attempt with that instrument,
then method replacement supplies `replacement-token` and reopens settlement. Evidence observes
both provider inputs, one recorded failure and exactly one eventual capture. A retry that merely
replays the declined instrument, or a replacement that fails to reopen the intent, is detected.
The tag is `example`, as required for this standard claim.

## Claim: receipt-explains-payment-state
Verdict: sound
Fingerprint: c98589bb183a1c72
Judged: 2026-08-08
Judge: codex

The e2e completes a trip without invoking the dispatch endpoint, waits for the settlement worker,
and opens the rendered receipt through the rider BFF. It asserts the named captured state and fare;
the component table ranges over pending, captured and declined service projections. The declined
copy states the next action in text, so color is not the only carrier. The tag remains `example`:
it demonstrates representative states and does not claim a universal accessibility proof. The
manual charter is intentionally not counted because it has no execution receipt.

## Claim: malformed-intent-does-not-starve-batch
Verdict: sound
Fingerprint: ec13aa51e11290a4
Judged: 2026-08-08
Judge: codex

The component test stores an invalid signed quote ahead of two valid intents in real PostgreSQL,
dispatches once, and observes one terminal failure plus both valid captures. A second dispatch
proves the poison intent left the pending set. Removing per-intent isolation, failure recording or
the dispatch marker fails a distinct assertion. The `example` tag matches the standard floor and
does not claim all malformed payloads or interleavings.
