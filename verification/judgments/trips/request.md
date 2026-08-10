# Judgments: trips/request

Revalidated 2026-08-10 for rider referral rewards. `RequestRide` now performs first-admission,
referral-code and optional credit reservation work inside the existing admission transaction. The
quote is still decoded and expiry-checked before trip persistence; quote and active-rider indexes
remain final concurrency guards. Each stale test and the complete handler, including rollback
paths, was re-read before refreshing these four verdicts.

Re-judged 2026-08-10 after D28 exposed realization sources. `RequestRide` authenticates and expires
quotes, enforces both uniqueness rules, creates the requested trip and returns its facts; the rider
route and view model preserve the relevant admission/refusal and acknowledgement outcomes. Every
remaining site therefore establishes a named part of its predicate.

Revalidated 2026-08-10 after quote validation moved from a feature-local design entry to the
reusable `security/quote-tokens` concern. Every stale covering body, the admission handler and both
index definitions were re-read. The handler still invokes the same decoder before writing, and the
new canonical decoding check narrows malformed input, so the existing verdict rationales remain
applicable while the design ownership becomes more accurate.

Revalidated 2026-08-10 after D27 added stable mechanism ids. The semantic diff changed no
enforcement, binding, expectation, rationale, claim, evidence form or source; the prior verdict
rationales therefore remain applicable and only their freshness fingerprints moved.

Re-judged 2026-08-08 after admission acquired aggregate version one and an atomic requested-event
outbox write. All admission evidence passed against the expanded real schema. The new unique
`(trip_id, version)` event constraint cannot satisfy either existing trip uniqueness rule on their
behalf; the quote and active-rider indexes remain the mechanisms those tests isolate.

Rebased 2026-08-08 after criticality entered claim freshness. No level, evidence site, required
form or verdict changed.

Re-judged 2026-08-08 after design bindings, migration metadata and bound source entered the
freshness fingerprint. Quote decoding, both unique-index shapes and the admission constructor were
read directly; the existing verdict rationales remain applicable.

**Re-judged 2026-08-08 for signed quote admission.** The superseded
verdicts were four `dishonest-tag` and two `toothless`; they are quoted in each entry rather than
deleted, because the point of the first pass is lost if the record only shows the state after the
fix.

**Conflict of interest, stated.** The same judge wrote the first verdicts, the rewritten tests and
these verdicts. Reading a test one wrote is worth little. What carries the three verdicts below that
previously failed is **mutation**: the implementation was broken in a specific way, the test was
run, and the failure observed. Where a verdict rests on reading rather than mutation, it says so.

The new pass checked the token codec, handler and `ux_trip_quote` source in addition to every test
body. Pricing is now a real process, so the earlier finding that no contract boundary existed no
longer describes the system.

## Claim: request-admitted-with-valid-quote
Verdict: sound
Fingerprint: 537554f394ce2425
Judged: 2026-08-10
Judge: codex

*(supersedes `dishonest-tag` — "two covering tests, both declaring `invariant`, both scripting a
single case")*

`A_valid_quote_admits_a_request_and_creates_one_trip_carrying_its_total` now runs 24 cases — three
currencies × eight signed amounts — each with a fresh rider, and reads the expected total from the
token fixture rather than writing it into the test. The e2e is tagged `example`,
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
Fingerprint: 6abdcc98ece2cfc4
Judged: 2026-08-10
Judge: codex

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
Fingerprint: 0f366cdbe9012615
Judged: 2026-08-10
Judge: codex

*(supersedes `dishonest-tag` — "declares an oracle the test does not use, about a boundary that does
not exist in the fixture")*

Two things moved, and only one was the test.

`An_unrecognised_quote_is_refused_whatever_identifier_is_offered` includes a byte alteration of an
otherwise valid signed token plus malformed encodings. Every case must return the same stable
refusal and create no trip. `Oracle.Contract` is now accurate: Pricing and Trips compile against the
same opaque token codec but run as different processes. Removing signature verification admits the
altered case; treating malformed input as a server error fails the response assertion.

## Claim: quote-consumed-once
Verdict: sound
Fingerprint: e3737547696a2675
Judged: 2026-08-10
Judge: codex

Re-judged unchanged after the file was rewritten; the test itself was not modified. Eight concurrent
requests at one quote, five trials, real Postgres, exactly one success and the right refusal code on
every other. Eight distinct riders, so the per-rider index cannot pass it on the quote rule's
behalf.

The mechanism changed with the process split: `ux_trip_quote` is a unique index on the quote id
stored with each trip. Dropping it lets concurrent consumers insert more than one trip and fails the
success and row counts; no write to Pricing is required.

## Claim: trip-created-in-requested-state
Verdict: sound
Fingerprint: 8e3e1762d6fb539a
Judged: 2026-08-08
Judge: codex

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
Fingerprint: 02685a6706fed16d
Judged: 2026-08-08
Judge: codex

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
Fingerprint: 0703bf73d59945d0
Judged: 2026-08-10
Judge: codex

Re-judged after a strengthening the first pass asked for. Eight concurrent requests for one rider
against eight distinct quotes, five trials; exactly one success and exactly one stored trip. The
refusals are now inspected rather than only counted, so a rejection carrying the wrong reason — the
weakness recorded last time — no longer passes.

`AdmitAsync` contains no application-level active-trip check, so `ux_trip_rider_active` is the
entire mechanism; without it all eight insert and the count fails. Verified by reading.

## Claim: request-admitted-after-terminal
Verdict: sound
Fingerprint: 763cb68d486fbea6
Judged: 2026-08-10
Judge: codex

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
