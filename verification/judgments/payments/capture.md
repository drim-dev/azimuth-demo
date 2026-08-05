# Judgments: payments/capture

## Claim: capture-created-on-completion
Verdict: sound
Fingerprint: f8d6deba835009ec
Judged: 2026-08-05
Judge: claude-opus-5

`A_completed_trip_is_captured_for_its_fare` writes an intent, dispatches, and asserts a capture
exists with the intent's amount and currency. Tried against a dispatcher that never inserts: the
`FindAsync` assertion fails. Against one that inserts a hard-coded amount: the amount assertion
fails. It discriminates on both halves.

## Claim: no-capture-before-completion
Verdict: sound
Fingerprint: cfb12b072e27bd28
Judged: 2026-08-05
Judge: claude-opus-5

Re-judged after the test was rewritten. `A_trip_that_has_not_completed_has_no_capture` now runs a
completed trip beside an in-flight one through the same dispatcher, asserts the completed one has a
capture and the in-flight one does not, and dispatches again to show the absence is not a timing
accident. Against a dispatcher that captured on any id it was handed, the second assertion fails.
It discriminates.

The previous version asked whether a freshly generated id was in an empty set, and passed against a
dispatcher that captured everything.

## Claim: no-capture-on-cancellation-without-fee
Verdict: toothless
Fingerprint: 65e103658f4d275b
Judged: 2026-08-05
Judge: claude-opus-5

**Still toothless after the fix, which is the finding.**
`A_cancelled_trip_with_no_fee_gets_no_capture_while_a_completed_one_does` is better than what it
replaced: it runs a cancelled trip beside a completed one and would fail against a dispatcher that
captured indiscriminately. But `CancelWithoutFeeAsync` is a no-op stub. The test still never
cancels anything, because *cancellation lives in the trip service and payments cannot see it*.

What the claim actually asserts spans two services: the trip service must not write a capture
intent when a trip cancels without a fee. No component test inside payments can establish that —
the honest evidence is at `e2e` scope, and this claim's plan entry should raise it once the driver
path exists to cancel through.

Recorded rather than papered over. The rewrite made the test better and did not make the verdict
sound, and an author who wanted a green matrix would have stopped at the rewrite.

## Claim: duplicate-completion-event
Verdict: sound
Fingerprint: c32de98e8ae989ba
Judged: 2026-08-05
Judge: claude-opus-5

`A_completion_delivered_any_number_of_times_captures_once` writes the intent six times and
dispatches after each, then counts rows. Tried against a dispatcher without the pre-check: the
unique index rejects the second insert and the count stays at one, so the test passes — correctly,
because the index is what the claim rests on. Tried against one with neither pre-check nor index:
the count reaches six and the test fails. It discriminates on the mechanism that matters.

## Claim: concurrent-completion-processing
Verdict: sound
Fingerprint: 8b3bea4e9ceed097
Judged: 2026-08-05
Judge: claude-opus-5

`Concurrent_workers_create_exactly_one_capture` fires eight captures at once and asserts exactly
one reports having won *and* exactly one row exists. Both halves matter: the row count alone would
pass if every worker silently lost, and the winner count alone would pass if two rows were written
and one worker misreported. Against a dispatcher with only the pre-check, the race writes several
rows and the count fails. Repeated five times, which is thin for a race but not nothing.

## Claim: retry-after-transport-failure
Verdict: sound
Fingerprint: 7b1a7f44eafc1957
Judged: 2026-08-05
Judge: claude-opus-5

`A_retry_after_an_unobserved_outcome_still_captures_once` scripts the provider to return
`Unobserved` first and `Captured` after, then retries four times. Against an implementation that
treats `Unobserved` as failure and retries the provider, the count reaches two and the test fails —
which is the double-charge this claim exists to prevent. It constructs the failure case rather than
assuming it away.

## Claim: capture-equals-trip-fare
Verdict: sound
Fingerprint: 10b032dff9f490e2
Judged: 2026-08-05
Judge: claude-opus-5

Re-judged after the test was rewritten. `A_completed_trip_is_captured_for_whatever_its_fare_is`
now generates 36 amounts across three currencies and asserts the captured amount and currency match
the intent each time. The `Invariant` tag now describes the test.

The previous version declared `Invariant` and exercised one amount — 1500 EUR. It was an example
wearing an invariant's tag, and it carried that tag because the `critical` standard demands
`invariant` and the cheapest way to satisfy a standard is to describe the test inaccurately. The
machine tier cannot see that: it compares the declared form to the required form and finds them
equal.

## Claim: adjusted-capture-records-reason
Verdict: sound
Fingerprint: 8bd0aaee85af8fe7
Judged: 2026-08-05
Judge: claude-opus-5

Re-judged after the test was rewritten. `An_adjusted_capture_records_whatever_reason_applies` now
generates 24 combinations of adjustment and reason and asserts both are recorded. The tag describes
the test.

The previous version tested one adjustment, one amount, one reason under an `Invariant` tag — the
same shape and the same cause as `capture-equals-trip-fare`.

## Claim: declined-capture-recorded
Verdict: sound
Fingerprint: b01b8bda259fd4f6
Judged: 2026-08-05
Judge: claude-opus-5

`A_decline_is_recorded_rather_than_dropped` asserts three things: the call reports failure, no
capture exists, and the failure row names the reason. An implementation that swallowed the decline
silently fails the third; one that captured anyway fails the second. Tagged `Example`, and it is
one — the tag is honest.

## Claim: declined-capture-is-retryable
Verdict: sound
Fingerprint: 60b58768668bbcb3
Judged: 2026-08-05
Judge: claude-opus-5

`A_declined_capture_may_be_retried_and_still_lands_at_most_once` declines, retries, and asserts one
capture and one recorded failure. Against an implementation that refused to retry after a decline
the second call fails; against one that lost the failure record the `Single` assertion fails.
Tagged `Example`, and it is one.
