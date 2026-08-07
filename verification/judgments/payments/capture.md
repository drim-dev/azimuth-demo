# Judgments: payments/capture

**Re-judged 2026-08-07.** The 2026-08-05 verdicts went stale when D19 renamed a tag value across
every evidence file. Re-reading them turned up something the first pass did not: **the trip service
writes no capture intent, and the design said it did.**

`design/payments/capture.md` named `CompleteTrip` as writing a capture-intent row in the same
transaction as the state change. No such site exists. The trip service has no reference to payments,
no intent table and no outbox; `WriteCaptureIntent` lives in payments, has no endpoint, and is
referenced by nothing. The design is corrected and marked.

So this spec's evidence establishes the *reading* half of a two-step chain — an intent produces at
most one capture, for the right amount, once. The *writing* half is claimed by nothing and built by
nothing. Eight verdicts below are unaffected by that, because they are about what payments does with
an intent it has. Two are not.

**Conflict of interest:** the same judge wrote the first verdicts and these.

## Claim: capture-created-on-completion
Verdict: spec-gap
Fingerprint: 723f4b11c5cdde02
Judged: 2026-08-07
Judge: claude-opus-5

*(supersedes `sound` — "writes an intent, dispatches, and asserts a capture exists with the intent's
amount and currency… it discriminates on both halves")*

That reasoning was right about the test and wrong about the claim. The scenario says *"WHEN the trip
reaches the completed state, THEN a capture is created"*, and nothing in the corpus connects those
two events. `A_completed_trip_is_captured_for_its_fare` seeds an intent row directly into the
payments database and dispatches. It discriminates well against a broken dispatcher — an inserter
that does nothing fails, one that hard-codes an amount fails — and not at all against a system where
completing a trip never produces an intent, which is the system that exists.

The code is right for what it covers and the test is toothy for what it covers. What a reader would
be surprised by is that **no claim anywhere says completion emits a capture intent**, so the two
halves are never joined by anything — not by a spec, not by a design entry that survives contact
with the source, not by a test. That is a gap in intent, which is why this is `spec-gap` rather than
`toothless`.

Closing it means a claim in `trips/lifecycle` that completion emits an intent, or building the
outbox the design describes. Recorded as a plan residual, marked "recorded, not accepted".

## Claim: no-capture-before-completion
Verdict: sound
Fingerprint: 80d00ca6d5e6803e
Judged: 2026-08-07
Judge: claude-opus-5

Unchanged. `A_trip_that_has_not_completed_has_no_capture` runs a completed trip beside an in-flight
one through the same dispatcher, asserts the completed one has a capture and the in-flight one does
not, and dispatches again to show the absence is not a timing accident. Against a dispatcher that
captured any id handed to it, the second assertion fails.

Within payments' boundary "has not completed" means "has no intent", and the test constructs exactly
that. The boundary caveat above applies and is recorded there rather than repeated here.

## Claim: no-capture-on-cancellation-without-fee
Verdict: toothless
Fingerprint: b0910b8726735e7d
Judged: 2026-08-07
Judge: claude-opus-5

**Still toothless, and the reason is now deeper than D18.2 recorded.**

`CancelWithoutFee` is a no-op stub whose own comment claims it makes the test "exercise the path
rather than assuming it". It exercises nothing: the cancelled trip is simply an id with no intent,
so the assertion that it has no capture holds against any implementation whatsoever, including one
that would capture a cancellation if a cancellation ever reached it.

D18.2 diagnosed this as a scope problem — the claim spans two services, so payments cannot establish
it. That stands, and the further fact is that **the path does not exist to raise the scope to**. The
trip service has no way to tell payments anything, so there is no e2e at which this becomes
checkable, and no amount of rewriting inside payments will help.

The honest resolutions are both structural: build the outbox, or move the claim to whichever spec
owns the trip service's obligation. Left as an error on the board rather than dressed up, which is
what D18.2 decided the first time this came up.

## Claim: duplicate-completion-event
Verdict: sound
Fingerprint: 8315046774058e34
Judged: 2026-08-07
Judge: claude-opus-5

`A_completion_delivered_any_number_of_times_captures_once` writes the intent six times and dispatches
after each, then counts rows. Against a dispatcher without the pre-check the unique index rejects the
second insert and the count stays at one — correctly, because the index is what the claim rests on.
Against one with neither pre-check nor index the count reaches six and it fails. It discriminates on
the mechanism that matters, and the redelivery axis is the one the claim quantifies over.

## Claim: concurrent-completion-processing
Verdict: sound
Fingerprint: 7d4ee0fbc51840a9
Judged: 2026-08-07
Judge: claude-opus-5

`Concurrent_workers_create_exactly_one_capture` fires eight captures at once and asserts exactly one
winner *and* exactly one row. Both halves matter: the row count alone passes if every worker silently
loses, the winner count alone passes if two rows are written and one worker misreports. Against a
dispatcher with only the pre-check the race writes several rows and the count fails. Five trials is
thin for a race and is stated as such rather than hidden.

## Claim: retry-after-transport-failure
Verdict: sound
Fingerprint: c6fa00a82f771be9
Judged: 2026-08-07
Judge: claude-opus-5

`A_retry_after_an_unobserved_outcome_still_captures_once` scripts the provider to return `Unobserved`
first and `Captured` after, then retries four times. Against an implementation treating `Unobserved`
as failure and retrying the provider, the count reaches two and it fails — which is the double charge
the claim exists to prevent. It constructs the failure case rather than assuming it away.

## Claim: capture-equals-trip-fare
Verdict: sound
Fingerprint: 09ab22ab7c362d0c
Judged: 2026-08-07
Judge: claude-opus-5

`A_completed_trip_is_captured_for_whatever_its_fare_is` generates 36 amounts across three currencies
and asserts the captured amount and currency match the intent each time. The generation is not what
makes it sound; the assertion referring to the generated value is — a hard-coded expectation cannot
distinguish copying from returning a constant.

Recorded again because it is the corpus's clearest before/after: the previous version exercised one
amount, 1500 EUR, under a `universal` tag.

## Claim: adjusted-capture-records-reason
Verdict: sound
Fingerprint: 6f674cf2a5be5999
Judged: 2026-08-07
Judge: claude-opus-5

`An_adjusted_capture_records_whatever_reason_applies` generates 24 combinations of adjustment and
reason and asserts both are recorded. Against an implementation that applied the adjustment and
dropped the reason, the second assertion fails on every combination. The tag describes the test.

## Claim: declined-capture-recorded
Verdict: sound
Fingerprint: 89217618e4ec2ff0
Judged: 2026-08-07
Judge: claude-opus-5

Asserts three things: the call reports failure, no capture exists, and the failure row names the
reason. An implementation that swallowed the decline fails the third; one that captured anyway fails
the second. Tagged `Example`, and it is one — the claim is `standard`, whose floor is `example`, so
nothing is owed and nothing is inflated.

## Claim: declined-capture-is-retryable
Verdict: sound
Fingerprint: e9605632c162fe85
Judged: 2026-08-07
Judge: claude-opus-5

Declines, retries, and asserts one capture and one recorded failure. Against an implementation that
refused to retry after a decline the second call fails; against one that lost the failure record the
`Single` assertion fails. Tagged `Example`, and it is one.
