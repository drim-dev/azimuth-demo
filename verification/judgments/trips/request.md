# Judgments: trips/request

## Claim: request-admitted-with-valid-quote
Verdict: dishonest-tag
Fingerprint: bcfae2ae627368df
Judged: 2026-08-06
Judge: claude-opus-5

Two covering tests, both declaring `invariant`, both scripting a single case.
`A_valid_quote_admits_a_request_and_creates_one_trip` issues one quote through `client.QuoteId()` —
which takes every default in `Api.IssueQuote`, so it is always `a → b`, 1000 + 500, EUR — and
submits one request for it. The e2e test at `e2e.test.ts:198` does the same through the rider app
with `requestedTrip`, one fixed quote shape again.

The claim ranges over every request referencing an unexpired quote. The evidence covers one point of
that space, twice. The honest tag is `example`, which for a `critical` claim would fail `wrong-form`
against the standard — which is the information the tag is currently suppressing.

Teeth are adequate for the positive half: against a handler that refuses everything, both tests
fail. This is not a toothless verdict; it is the tag.

The fix has precedent in `payments/capture#capture-equals-trip-fare`: range over quote parameters —
several fares, currencies and times-to-expiry — and assert admission holds across them. Then the tag
is true.

## Claim: request-rejected-with-expired-quote
Verdict: dishonest-tag
Fingerprint: bec643e80a1a4a59
Judged: 2026-08-06
Judge: claude-opus-5

`An_expired_quote_is_refused_and_creates_nothing` advances the clock 3 minutes against a 2-minute
`IssueQuote.Validity`, so it probes exactly one point: one minute past expiry. Declared `invariant`.

Tried against a handler with the expiry check deleted: the test fails, so it is not toothless. Tried
against `quote.ExpiresAt.AddMinutes(5) <= now` — a grace window, the mutation a single deep-in-the-
region sample cannot see — the test passes and expired quotes are admitted. That mutation is what
the missing quantification costs, and it is why the tag matters here rather than being bookkeeping.

An honest `invariant` ranges over the offset: quotes expired by a second, a minute, an hour, and
(for the other side of the boundary) unexpired ones admitted right up to it.

## Claim: request-rejected-with-unknown-quote
Verdict: dishonest-tag
Fingerprint: afb1792c0b2fcbc0
Judged: 2026-08-06
Judge: claude-opus-5

The sharpest of the four, because the plan's own stated risk is not exercised at all.

`verification/trips/request.md` requires `Oracle: contract` and gives the reason: *"the failure mode
is a disagreement between two services about what 'unknown' looks like"*. The test declares
`Oracle.Contract`. It contains no contract check, and there is no second service to disagree with:
quotes are issued by `Trips.Features.Quotes.IssueQuote` on the trip service's own `/quotes` route
and stored in `TripDbContext.Quotes`. `Pricing` is a class library — `Money.cs` — not a service.
`AdmitAsync` answers "unknown" from a local `db.Quotes` read.

So the tag declares an oracle the test does not use, about a boundary that does not exist in the
fixture. `Invariant` is the second mis-declaration: `Random.Shared.NextInt64()` is one sample per
run, not a range.

Two things need to move, and only one of them is the test. Either the plan entry is describing an
architecture the fixture does not have and should be rewritten to say what it actually rests on, or
the fixture owes the split it assumes. Recorded as a tag verdict because that is what is checkable
today, but the plan entry is the thing that is wrong.

## Claim: quote-consumed-once
Verdict: sound
Fingerprint: 1cf02d99a024b47d
Judged: 2026-08-06
Judge: claude-opus-5

`A_quote_is_consumed_by_at_most_one_request_however_many_arrive_together` fires 8 concurrent
requests at one quote, 5 trials, and asserts exactly one 200, the right refusal code on every other,
and 5 trips total across the run. Against real Postgres through the `DatabaseHarness`.

Tried against the conditional update made unconditional — `Where(q => q.Id == quoteId)` without the
`ConsumedByTripId == null` clause — every request sees one affected row, all 8 are admitted, the
count reaches 8 and the test fails. Tried against the application pre-check alone: the concurrent
arrivals all read `ConsumedByTripId is null` together and the same failure follows. It discriminates
on the mechanism the design says is load-bearing.

The variation axis is the one the claim ranges over, so `invariant` is honest, and `component` is
honest because the store is real. Worth noting deliberately: the 8 requests use 8 *distinct* riders,
so `ux_trip_rider_active` cannot do this test's work for it. That isolation is what makes the
verdict sound rather than lucky.

## Claim: trip-created-in-requested-state
Verdict: toothless
Fingerprint: e0152b44650616ee
Judged: 2026-08-06
Judge: claude-opus-5

The scenario asserts three things. `A_valid_quote_admits_a_request_and_creates_one_trip` checks one
of them properly.

- *"exactly one trip is created"* — never counted. The test reads back the single trip named by the
  response id. Against a handler that inserted a duplicate row, every assertion still passes. The
  count is available and used elsewhere in the same file (`fixture.Database.Count<Trip>`), and the
  database is cleared per test, so this is a missing assertion rather than a hard one.
- *"the trip carries the total from the referenced quote"* — asserted as `1500`, a literal. Since
  `client.QuoteId()` always issues the same quote, a handler returning a hard-coded 1500 passes.
  The test cannot distinguish "copies the quote total" from "returns a constant", which is the whole
  content of the claim.
- *"in the requested state"* — checked on both the response and the stored row. This one has teeth.

Also tagged `Invariant` where the `standard` level requires only `example`; over-declaring on a
single hard-coded case. The toothlessness is the primary defect, but it has the same root: one
fixed quote cannot establish a claim about carrying *the quote's* value.

## Claim: rider-informed-of-trip
Verdict: dishonest-tag
Fingerprint: 9d0d729877d584f2
Judged: 2026-08-06
Judge: claude-opus-5

`e2e.test.ts:196` asserts the rider's view shows `state: 'requested'` and `awaitingDriver: true`,
through real process boundaries. The identifier half is covered indirectly but genuinely:
`requestedTrip` fails if `created.body.id` is absent, since the subsequent GET would not resolve.
Against a rider app that dropped `awaitingDriver`, the test fails. Not toothless.

Declared `invariant` on one scripted path. The `standard` level requires only `example`, so the
honest tag satisfies the standard as it stands — this one costs nothing to fix, which is what makes
it worth recording. It is not a test that was hard to write honestly; it is a tag written on
autopilot. Every `covers` call in `e2e.test.ts` says `invariant`, all seventeen of them, which is
the pattern rather than this claim.

## Claim: second-request-rejected-while-active
Verdict: sound
Fingerprint: e5d504d4b7014d8e
Judged: 2026-08-06
Judge: claude-opus-5

`A_rider_holds_at_most_one_active_trip_however_many_requests_arrive_together` fires 8 concurrent
requests for one rider against 8 distinct quotes, 5 trials, asserting exactly one 200 and exactly
one trip row for that rider.

`AdmitAsync` contains no application-level active-trip check at all, so `ux_trip_rider_active` is
the entire mechanism. Tried without the index: all 8 requests insert, the per-rider count reaches 8
and the test fails. The distinct quotes matter for the same reason the distinct riders matter in
`quote-consumed-once` — the quote rule cannot stand in for the rider rule here.

One weakness recorded, short of a verdict: the refusals are counted, not inspected. A handler that
answered 500 on a deadlock rather than `trip:request:create:rider_has_active_trip` would pass, where
the sibling quote test asserts the code on every refusal. The claim as written says only "rejected",
so this is the evidence being weaker than the sibling, not weaker than the claim.

## Claim: request-admitted-after-terminal
Verdict: toothless
Fingerprint: 5d364872360f8d9a
Judged: 2026-08-06
Judge: claude-opus-5

`A_rider_may_request_again_once_their_trip_is_terminal` cancels, then requests again. There are two
terminal states — `TripStateMachine.IsTerminal` is `Completed or Cancelled` — and the test exercises
one.

The mechanism is a partial index whose predicate is a SQL string literal:
`state NOT IN ('completed', 'cancelled')` in `TripConfiguration.cs`. Drop `'completed'` from that
filter and this test passes unchanged, while a rider whose trip completed normally can never request
another ride. That is a plausible wrong implementation, it breaks the claim, and the evidence does
not see it.

`design/trips/request.md` predicted exactly this: *"this index depends on the set of terminal states,
which `trips/lifecycle` owns. Adding a state and forgetting to classify it silently widens or
narrows this rule. Nothing currently catches that."* The prediction was written about future states
and is already true of the existing ones. The honest form enumerates terminal states from
`TripStateMachine` and drives a trip to each — which also makes the enumerator derived (D13.1)
rather than hand-picked, so a third terminal state would arrive already covered.
