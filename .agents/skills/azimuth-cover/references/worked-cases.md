# Worked cases

Fixture-local. Every citation here points into this repo's corpus and carries no normative content —
`SKILL.md` is complete without it. Delete this file when extracting the skill.

Each case names a real judgment in `verification/judgments/`, so the reasoning can be read beside
the code rather than taken on trust.

## Shape A — derived enumeration

**`trips/request#request-admitted-after-terminal`, judged `toothless`.**

`app/services/Trips.Tests/Features/Trips/RequestRideTests.cs:117` cancels a trip and requests again,
tagged `Quantification.Universal`. There are two terminal states —
`app/services/Trips/Domain/TripState.cs:63` — and it exercises one.

The mechanism is a partial index whose predicate is a SQL string literal in
`app/services/Trips/Database/Configurations/TripConfiguration.cs:53`. Drop `'completed'` from that
filter and the test still passes while every rider who completes a trip is locked out permanently.

The honest form is a loop over `TripStateMachine.States.Where(TripStateMachine.IsTerminal)` — **two
cases, fewer than a hand-written table**, and universal anyway because a third terminal state
arrives already covered. `design/trips/request.md` predicted this drift for future states; it was
already true of the existing two.

## Shape B — generated space, computed oracle

**Before — `trips/request#trip-created-in-requested-state`, judged `toothless`.**

`RequestRideTests.cs:34` asserts `trip.FareMinor.Should().Be(1500)`. The fixture helper issues one
quote shape, so 1500 is the only value the claim is ever checked against, and a handler returning a
constant passes. The claim is *"carries the total from the referenced quote"* — precisely the
relation a literal cannot express.

**After — `payments/capture#capture-equals-trip-fare`, judged `sound`.**

`app/services/Payments.Tests/Features/Captures/DispatchCapturesTests.cs:41-50` generates the amount,
seeds the fixture with it, and asserts against the same variable. The generation is not what makes
it sound; the assertion referring to the generated value is.

The judgment records what it replaced: one amount, 1500 EUR, under an `Invariant` tag — *"an example
wearing an invariant's tag, and it carried that tag because the `critical` standard demands
`invariant` and the cheapest way to satisfy a standard is to describe the test inaccurately"*.

### The boundary half

**`trips/request#request-rejected-with-expired-quote`, judged `dishonest-tag`.**

`RequestRideTests.cs:46` advances the clock 3 minutes against a 2-minute validity — one point, one
minute past expiry. Deleting the expiry check fails the test, so it is not toothless. Replacing the
comparison with a five-minute grace window passes it, and admits expired quotes.

Sampling the interior of "expired" would not catch that either. The edge has to be built: admitted
just inside, refused just outside.

## Shape C — contention

**`trips/request#quote-consumed-once` and `#second-request-rejected-while-active`, both `sound`.**

`RequestRideTests.cs:76` and `:99` each fire 8 concurrent requests, 5 trials, against real Postgres,
asserting exactly one success and that the store agrees.

The detail worth copying is the isolation. The quote test uses **8 distinct riders** so the
per-rider index cannot pass it; the rider test uses **8 distinct quotes** so the quote rule cannot.
Each isolates the mechanism its claim names. Without that, one constraint silently covers for the
other and both tags become unfalsifiable.

Thinness is admitted rather than hidden: the `payments/capture#concurrent-completion-processing`
judgment says five trials is *"thin for a race but not nothing"*.

## Shape D — one case, honestly

**`payments/capture#declined-capture-recorded`, judged `sound`, tagged `Example`.**

Three assertions over one scripted decline, at a `standard` claim whose floor is `example`. The
judgment ends *"Tagged `Example`, and it is one — the tag is honest."* Nothing was owed and nothing
was inflated.

## The refusal path, twice

**`trips/dispatch#expired-offer-withdrawn`** — `verification/trips/dispatch.md` sets
`Quantification: example` with `Residual: offer expiry is checked at a single boundary instant, not
across the range of clock skew…` and accepts it because *"clock-skew handling is not yet designed…
inventing evidence for an undesigned mechanism would be worse than recording the gap."*

**`trips/driver-view#rider-contact-confined-to-held-trips`** — `verification/trips/driver-view.md`
drops to `example` with `Residual: the class is checked structurally; no test enumerates every
driver-facing surface`, accepted because an enumeration of today's surfaces would restate the
structural check and rot the day one is added. This is the missing-enumerator case from `SKILL.md`'s
shape A, resolved by refusing rather than by hand-listing.

Both are plan entries. Neither is a tag.

## Baseline

At the time this skill was written, the agent tier had judged 18 claims across two specs:

| Spec | dishonest-tag | toothless | sound |
|---|---|---|---|
| `trips/request` | 4 | 2 | 2 |
| `payments/capture` | 2 (since fixed) | 1 | 7 |

Every tag failure in both passes was `Quantification`, or the `Oracle` beside it. Twenty claims
remain `unjudged`, which is the sample the skill's own falsifier can be measured against.

**No wrong-axis test has been observed yet.** `SKILL.md` warns about generation on an irrelevant
axis as the predicted failure of teaching the shapes; it is a prediction, and should be treated as
one until a judgment records it.
