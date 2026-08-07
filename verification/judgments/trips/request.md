# Judgments: trips/request

**Re-judged 2026-08-07 after the evidence was rewritten to answer the first pass.** The superseded
verdicts were four `dishonest-tag` and two `toothless`; they are quoted in each entry rather than
deleted, because the point of the first pass is lost if the record only shows the state after the
fix.

**Conflict of interest, stated.** The same judge wrote the first verdicts, the rewritten tests and
these verdicts. Reading a test one wrote is worth little. What carries the three verdicts below that
previously failed is **mutation**: the implementation was broken in a specific way, the test was
run, and the failure observed. Where a verdict rests on reading rather than mutation, it says so.

**Fingerprints refreshed 2026-08-07.** Every verdict below was re-affirmed rather than re-derived:
the evidence files changed for reasons belonging to other specs — tag corrections in shared test
files — and no test body carrying these claims was touched. The fingerprint expired because it
hashes whole files, which D19.1 records.

## Claim: request-admitted-with-valid-quote
Verdict: sound
Fingerprint: b34565e1c1a39bf8
Judged: 2026-08-07
Judge: claude-opus-5

*(supersedes `dishonest-tag` — "two covering tests, both declaring `invariant`, both scripting a
single case")*

`A_valid_quote_admits_a_request_and_creates_one_trip_carrying_its_total` now runs 24 cases — three
currencies × eight seeded amounts — each with a fresh rider, and reads the expected total back from
the quote rather than writing it into the test. The e2e at `e2e.test.ts:198` is retagged `example`,
which is what one scripted path through the assembled system is; the `critical` floor is met by the
component test, so nothing is weakened.

Teeth were not assumed. `FareMinor = quote.TotalMinor` was replaced with `FareMinor = 1500` and the
test failed. Against a handler that refused everything it fails on the first status assertion.

Re-affirmed 2026-08-07 after the shared e2e file changed for unrelated reasons — a driver-movement
assertion and a retag belonging to `trips/rider-view`. The evidence for this claim is untouched and
the verdict is unchanged; the fingerprint expired because it hashes whole files.

Recorded weakness: all 24 cases use the same pickup, dropoff and instant. The claim's own axis is "a
quote that has not expired", and the near side of that boundary is exercised by
`request-rejected-with-expired-quote` rather than here.

## Claim: request-rejected-with-expired-quote
Verdict: sound
Fingerprint: 6878eadb29534256
Judged: 2026-08-07
Judge: claude-opus-5

*(supersedes `dishonest-tag` — "probes exactly one point: one minute past expiry")*

`An_expired_quote_is_refused_however_far_past_expiry` ranges over six offsets from the expiry
instant — zero, one tick, one second, one minute, one hour, one day — computing each from the
`ExpiresAt` the service returned rather than from the two-minute validity constant. It then
constructs the near side: one tick *before* expiry, admitted.

The first pass named the mutation this had to catch and predicted the old test would survive it.
Applied `quote.ExpiresAt.AddMinutes(5) <= now` and the test failed. The near-side case is what makes
that work — every expired sample sits inside a five-minute grace window, so without it the grace
window still passes.

## Claim: request-rejected-with-unknown-quote
Verdict: sound
Fingerprint: dae597dc05d0d713
Judged: 2026-08-07
Judge: claude-opus-5

*(supersedes `dishonest-tag` — "declares an oracle the test does not use, about a boundary that does
not exist in the fixture")*

Two things moved, and only one was the test.

`An_unrecognised_quote_is_refused_whatever_identifier_is_offered` now ranges over sixteen
identifiers across both ways of failing to name a quote: twelve well-formed but absent ids, and four
that do not decode at all. Both must reach the rider as the same refusal code, which is the part a
client branches on.

`Oracle.Contract` is gone from the tag, and the plan entry that demanded it is rewritten with the
supersession marked. It had required a contract oracle because "the failure mode is a disagreement
between two services", and there are no two services — quotes are issued by the trip service's own
`/quotes` slice, and `Pricing` is `Money.cs`. Verified by reading, not by mutation.

The general finding is worth more than this claim: **a plan can require a form for a reason that
never existed, the tag can copy the requirement, and both look correct to `azimuth check`** —
`Oracle` is descriptive and never gated, so nothing compares it to anything.

## Claim: quote-consumed-once
Verdict: sound
Fingerprint: 8647fda4ade88fea
Judged: 2026-08-07
Judge: claude-opus-5

Re-judged unchanged after the file was rewritten; the test itself was not modified. Eight concurrent
requests at one quote, five trials, real Postgres, exactly one success and the right refusal code on
every other. Eight distinct riders, so the per-rider index cannot pass it on the quote rule's
behalf.

Against the conditional update made unconditional, every request sees one affected row, all eight
are admitted and the count assertion fails. Verified by reading in the first pass; not re-mutated.

## Claim: trip-created-in-requested-state
Verdict: sound
Fingerprint: a1641921fcfc9635
Judged: 2026-08-07
Judge: claude-opus-5

*(supersedes `toothless` — "exactly one trip is created — never counted; carries the total from the
referenced quote — asserted as `1500`, a literal")*

Both holes are closed by the same rewrite. The trip count is now asserted after every admission and
compared against the number admitted so far, so a duplicate insert fails rather than passing
unnoticed. The fare is asserted against the quote's returned total across 24 differing quotes, so a
constant cannot satisfy it — confirmed by mutation, as above.

Tagged `Universal` where `standard` requires only `example`. That is above the floor and now true,
where before it was above the floor and false.

## Claim: rider-informed-of-trip
Verdict: sound
Fingerprint: 31045810d0df5c52
Judged: 2026-08-07
Judge: claude-opus-5

*(supersedes `dishonest-tag` — "declared `invariant` on one scripted path… a tag written on
autopilot")*

The test is unchanged and the tag is now `example`, which describes it. `standard` requires
`example`, so the honest tag satisfies the standard as it stands — which was the first pass's point:
this one cost nothing to fix.

`e2e.test.ts:196` asserts the rider's view shows `state: 'requested'` and `awaitingDriver: true`
through real process boundaries, and the identifier half is covered because `requestedTrip` cannot
proceed without an id. Verified by reading.

Re-affirmed 2026-08-07: the file changed for reasons belonging to another spec, this test did not,
and the verdict stands.

## Claim: second-request-rejected-while-active
Verdict: sound
Fingerprint: 6ee2d22e5affecd5
Judged: 2026-08-07
Judge: claude-opus-5

Re-judged after a strengthening the first pass asked for. Eight concurrent requests for one rider
against eight distinct quotes, five trials; exactly one success and exactly one stored trip. The
refusals are now inspected rather than only counted, so a rejection carrying the wrong reason — the
weakness recorded last time — no longer passes.

`AdmitAsync` contains no application-level active-trip check, so `ux_trip_rider_active` is the
entire mechanism; without it all eight insert and the count fails. Verified by reading.

## Claim: request-admitted-after-terminal
Verdict: sound
Fingerprint: 42dfddab6442d5f1
Judged: 2026-08-07
Judge: claude-opus-5

*(supersedes `toothless` — "there are two terminal states and the test exercises one… drop
`'completed'` from that filter and this test passes unchanged")*

`A_rider_may_request_again_from_any_terminal_state` iterates
`TripStateMachine.States.Where(TripStateMachine.IsTerminal)` and drives a trip to each — cancel for
one, accept/start/complete for the other — asserting the stored state before requesting again. Two
cases, fewer than a hand-written table would contain, and universal because the enumeration is the
system's rather than the author's. A member with no path throws rather than being skipped, so a
third terminal state fails loudly for want of evidence instead of quietly passing.

The mutation the first pass predicted was applied: `'completed'` was dropped from the
`ux_trip_rider_active` filter in the migration, and the test failed. That is the drift
`design/trips/request.md` says nothing currently catches. Something now does.
