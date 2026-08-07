# Judgments: trips/rider-view

## Claim: position-confined-to-live-phases
Verdict: dishonest-tag
Fingerprint: 8844393715188520
Judged: 2026-08-07
Judge: claude-opus-5

The site-class claim, and the only one in the corpus whose domain is a set of sites rather than
executions. `e2e.test.ts:226` visits five surfaces — the trip API view, the receipt API, the trip
service's raw `/trips/{id}/driver` route, and the two rendered pages — and asserts `52.37` appears in
none of them after completion.

As a regression test it is good, and it constructs the real historical failure: the receipt is the
surface that leaked, and the test asserts its absence at both the API and the rendered page.

The tag is the problem. It declares `universal` over the site class while the test **hand-lists five
URLs**. The claim itself says membership is "derived from what the code built: a new rider-facing
site joins the class by being written, without anyone remembering to add it". A support view, an
analytics export or a push payload added tomorrow is a member of the class and is not visited here —
which is the exact failure the claim was written against, and which the plan's residual
`rider-reachable-surface` already predicts in those words.

D13.1's rule applies directly: a hand-listed enumeration of a derived domain is worse than none,
because it reports green over an unknown fraction. What makes this survivable is that the class *is*
derived elsewhere — from `Realizes` tags, checked as `invariant-breach` — so the model knows the
membership even though the test cannot. **The honest reading is that the universality of this claim
rests on the derived enumerator and the type-level enforcement, not on this test**, and the tag
claims for the test what only those can do.

The fix is not a bigger test. It is to tag this `example` — it is a five-member regression — and let
the claim's universal evidence be the mechanism it already has in `design/trips/rider-view.md`:
`DriverPosition` with no serializer, plus the choke-point that no rider-facing route returns a
position at all.

## Claim: no-driver-identity-before-assignment
Verdict: sound
Fingerprint: abcf117734a99f40
Judged: 2026-08-07
Judge: claude-opus-5

`Before_assignment_no_individual_driver_is_shown` asserts display, position and vehicle are all null
at `TripState.Requested`; `e2e.test.ts:148` asserts the same through the assembled path and adds a
substring check that `52.37` appears nowhere in the payload.

Against a projection with `DriverDisplay: driverDisplay` unconditional, both fail. Against one that
leaked only through a differently-named field, the e2e substring assertion still fails. It
discriminates.

The axis the claim quantifies over is the phase, and "not yet assigned" has exactly one member
today, so `universal` is accurate — it exhausts the domain. Two weaknesses recorded, neither
sufficient to fail it:

- **The doc comment above the test is false of the test.** It says "Quantified over every phase
  rather than over the three the spec names, so a state added later is covered on the day it is
  added." The body hard-codes `TripState.Requested`. That sentence describes
  `After_a_terminal_state_the_name_remains_and_the_position_does_not`, which does derive its set.
- Because the phase is hard-coded, the tag stops being true silently if a pre-assignment state is
  ever added. The same file shows the alternative eight lines below.

## Claim: no-driver-position-before-assignment
Verdict: sound
Fingerprint: e973acdd12e233cc
Judged: 2026-08-07
Judge: claude-opus-5

Same two tests. The plan raises this claim to `e2e` on the argument that each site can pass in
isolation while the composition leaks, and the e2e evidence exists and checks the assembled path
rather than the projection alone.

Against `DriverPosition: position?.Reveal()` unconditional, the unit test fails on the null
assertion and the e2e fails on both the field and the substring check. The same hard-coded-phase
fragility as above applies and is recorded there.

## Claim: supply-density-shown-before-assignment
Verdict: toothless
Fingerprint: 3d312235e25ff21b
Judged: 2026-08-07
Judge: claude-opus-5

The claim's normative content is *"identifies no individual driver"*. Neither test addresses it.

`Before_assignment_only_coarse_density_is_shown` asserts `SupplyDensity == "moderate"` against a
fixture that passed `supplyDensity: "moderate"` in. It is a round-trip: it establishes that the
projection copies a string it was handed. The e2e asserts `typeof supplyDensity === 'string'`, which
is weaker still.

Against a projection that computed density as "1 driver at 52.37,4.89" the unit test fails only
because the literal differs, not because anything checked for identifiability — and the e2e passes
the `typeof` check outright. The substring assertion on `52.37` in the same e2e test is what would
catch that particular leak, and it is tagged to the two sibling claims, not to this one.

`design/trips/rider-view.md`'s residue states the failure case outright: density is computed from
real driver positions and is not differentially private, so with few drivers it identifies an
individual. **The failure case is known, written down, and never constructed** — the trap the rubric
names.

Second, weaker point: the scenario says an indication "may be shown", which makes the observable
half optional while the normative half sits in a subordinate clause. A THEN that permits rather than
requires is hard to write toothy evidence against.

## Claim: driver-shown-after-assignment
Verdict: toothless
Fingerprint: 9bdb2a1aaf4e5f18
Judged: 2026-08-07
Judge: claude-opus-5

The scenario names three things shown: display name, **vehicle**, and current position. No test
anywhere checks the vehicle in the positive case.

`Between_assignment_and_a_terminal_state_the_driver_is_shown` asserts `DriverDisplay` and
`DriverPosition` over `{Assigned, InProgress}`. `e2e.test.ts:162` asserts `driver?.name` and
`driverPosition`. Neither reads a vehicle field.

Tried against `Vehicle: null` unconditional in `RiderProjection.For`: the before-assignment test
asserts vehicle is null and passes, the after-assignment tests never look, the terminal test never
looks. **The system could stop returning the vehicle entirely and every test in the corpus still
passes.**

Also recorded: `{Assigned, InProgress}` is hand-listed, where "once assigned and until terminal"
is derivable as the complement of the terminal set. A new live phase would silently escape.

## Claim: driver-position-follows-driver
Verdict: toothless
Fingerprint: 9aeabb46ee266c00
Judged: 2026-08-07
Judge: claude-opus-5

The claim is *"WHEN the assigned driver's position **changes**, THEN the position shown to the rider
follows it"*. The covering test never changes a position.

`e2e.test.ts:162` asserts `driverPosition === '52.37,4.89'` after assignment and again after start —
the same literal both times, because `Api.AvailableDriver` fixes it and no route in the fixture
moves a driver. A projection that returned a hard-coded `'52.37,4.89'` passes. So does one that
cached the first value and never updated.

This is the same shape as `payments/capture#no-capture-on-cancellation-without-fee` (D18.2): the
evidence cannot construct the trigger because the trigger does not exist in the fixture. The honest
resolution is therefore not a better test but a plan entry — `Residual:` that propagation is
unexercised because no driver-position update path exists, `Accepted:` with the condition that
introduces one. Recording it as toothless without that entry would leave the corpus looking merely
sloppy rather than structurally blocked.

## Claim: no-position-after-completion
Verdict: spec-gap
Fingerprint: a78bb4da885f2cea
Judged: 2026-08-07
Judge: claude-opus-5

The evidence for the scenario as written is fine. `After_a_terminal_state_the_name_remains_and_the
_position_does_not` iterates `TripStateMachine.States.Where(IsTerminal)` — a derived enumeration, so
`universal` is honest and a third terminal state arrives covered — and `e2e.test.ts:178` completes a
trip and asserts the position is gone from the assembled path. Against `DriverPosition: assigned ||
terminal ? … : null` both fail.

The gap is what the scenario does not say. `design/trips/rider-view.md` records **two** mechanisms
for this requirement, and the spec describes only the first:

1. `RiderProjection.For` returns no position for terminal phases — what a *request* returns.
2. `RiderTripStream.Close`, invoked by the state machine's terminal transition — what an
   already-open connection keeps pushing, "which the projection cannot reach", and which the design
   itself calls *"the interesting failure: a subscription that was correct while the trip ran and is
   never torn down."*

The scenario's WHEN is "the rider views the trip". A rider holding an open stream is not viewing;
they are being pushed to. No scenario in this spec covers that observation mode, so no evidence is
required for it and none exists. The design residue names a third path the spec is silent on: the
rider client restores a cached position on cold start after completion.

A reader who checked the matrix would conclude a completed trip cannot show a position. Two named
paths say otherwise. That is a gap in what the spec claims, not in what the tests do.

## Claim: no-position-after-cancellation
Verdict: spec-gap
Fingerprint: 3f2ff9054a0bdc31
Judged: 2026-08-07
Judge: claude-opus-5

The same requirement, so the same gap: stream teardown fires on the terminal transition, which
includes cancellation, and no scenario covers a pushed observation.

The scenario-level evidence is adequate but thinner than its sibling's, which is worth recording.
The unit test carries it: `View(Cancelled)` is constructed *with* a position and asserts it is not
projected. The e2e at `e2e.test.ts:185` cancels a trip that was never assigned, so no driver
position existed to leak and its null assertion would pass against almost anything. If the unit test
were removed, this claim would have no evidence that constructs the case that matters.

## Claim: driver-identity-remains-on-receipt
Verdict: sound
Fingerprint: 3217f30e0d48746d
Judged: 2026-08-07
Judge: claude-opus-5

The positive half of the terminal rule, and the one that stops the requirement being satisfiable by
returning nothing at all. The unit test asserts `DriverDisplay == "Sam"` across the derived terminal
set; the e2e asserts the name survives completion at the API and appears in the rendered receipt
page (`e2e.test.ts:256`).

Against `DriverDisplay: assigned ? driverDisplay : null` — the obvious over-redaction, and the
likely accident when someone tightens the rule above — the unit test fails on both terminal states
and the e2e fails on the receipt page. It discriminates in the direction that matters, which is the
one a privacy-motivated change would break.
