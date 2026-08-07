# Judgments: trips/lifecycle

First pass, 2026-08-07. Three component tags were corrected before judging: they declared
`universal` over one scripted trip, and every claim they carry is also covered by a unit test that
genuinely enumerates the machine, so the honest tag costs nothing and no floor is breached.

**This spec has the strongest evidence in the corpus**, and the reason is worth naming: the unit
tests enumerate `TripStateMachine.States` × `TripStateMachine.Events` — a derived enumeration, not a
list — and compare against an independently written `Permitted` set. The enumeration comes from the
system; the expected answer comes from a model that can disagree with it. That is what
`Oracle.ModelBased` is for.

**Conflict of interest:** the judge did not write these tests, but wrote the retagging.

## Claim: assigned-to-in-progress
Verdict: sound
Fingerprint: 73361cc907865cb2
Judged: 2026-08-07
Judge: claude-opus-5

Covered by `Exactly_the_permitted_pairs_are_accepted`, which walks every state–event pair and
asserts acceptance exactly where the model permits it. `(Assigned, Start)` is one cell of that
table, and it is checked in both directions: an implementation that refused it fails, and one that
accepted it from the wrong state fails on a different cell.

The whole-table form is what makes this stronger than a scenario test. A test asserting only that a
started trip moves to in-progress cannot notice that the same event is wrongly accepted elsewhere.

## Claim: in-progress-to-completed
Verdict: sound
Fingerprint: d713fe820b651ee1
Judged: 2026-08-07
Judge: claude-opus-5

The same table, the cell `(InProgress, Complete)`. Same reasoning, and the component tests exercise
the same transition against a real store on the way to every terminal case they set up, so the
transition is not only modelled but executed.

## Claim: unpermitted-transition-rejected
Verdict: sound
Fingerprint: d6f2673f8d17f7b7
Judged: 2026-08-07
Judge: claude-opus-5

The claim quantifies over "a trip in any state" and "a transition not permitted from that state",
and the evidence quantifies over exactly that: all twenty state–event pairs, asserting the
implementation's answer equals the model's for each.

Against an implementation that permitted one extra pair, that cell fails. Against one that permitted
everything, fourteen cells fail. `Universal` with a model-based oracle is honest, and the plan asks
for precisely this and gets it.

Recorded: the `Permitted` set is hand-written. That is correct here — it is the *oracle*, and an
oracle that agreed with the implementation by construction would be worthless. The *enumerator* is
derived, which is the part D13.1 constrains.

## Claim: no-transition-out-of-terminal
Verdict: sound
Fingerprint: 1732fff79d355d7b
Judged: 2026-08-07
Judge: claude-opus-5

Two tests, at two scopes, and both are needed.

`A_terminal_state_admits_no_event_at_all` iterates the derived terminal set × every event and
asserts the machine refuses all of them — universal over both axes.

`A_terminal_trip_admits_no_event_against_a_real_store` cancels a trip and then attempts every
transition verb over HTTP, asserting a conflict each time and that the stored state is unchanged.
Retagged `Example`, which is what one terminal state reached one way is; the universal form is
carried by the unit test above.

The pairing is the point: the pure function can be right while the handler reads a stale state, and
the component test is what excludes that.

## Claim: replayed-transition-is-inert
Verdict: sound
Fingerprint: cd2d4035f5ca9777
Judged: 2026-08-07
Judge: claude-opus-5

`A_replayed_transition_changes_nothing_however_many_times_it_arrives` completes a trip, then fires
six further completions concurrently, five trials, asserting every one is refused with a conflict,
the state is still completed, and — the assertion that matters — the history contains exactly one
completion.

Against a handler whose conditional write is not conditional, the history count reaches seven and
fails. Against one that swallowed replays without recording them, the count assertion still holds
and the conflict assertion fails, so both directions are covered.

The plan raises this to `component` on the argument that replay tolerance depends on a conditional
write against committed state, and the evidence is at that scope against real Postgres. `Universal`
is honest: the axis is arrival multiplicity and the test ranges over it.

## Claim: transition-records-actor-and-instant
Verdict: sound
Fingerprint: 6cabd01f3797485b
Judged: 2026-08-07
Judge: claude-opus-5

`History_only_grows_and_records_who_caused_each_move` asserts every recorded transition carries a
non-blank actor, that the last one is exactly `(in-progress, completed, driver-0)`, and that every
recorded instant equals the fixture's clock.

The instant assertion has more teeth than it looks: against a handler stamping `DateTimeOffset.Now`
instead of the injected clock, it fails — which is the ordinary way this claim gets quietly broken.

Retagged `Example`: one trip, one actor pair. `standard`'s floor is `example`.

## Claim: history-is-append-only
Verdict: sound
Fingerprint: a1d867ef83b39ed9
Judged: 2026-08-07
Judge: claude-opus-5

The same test, and the append-only half is checked structurally rather than by counting: the history
after two further transitions is asserted to *start with* the earlier history, element for element,
and to be exactly two longer.

Against an implementation that rewrote an earlier row — the failure the claim exists to exclude —
the prefix comparison fails while a count-only check would pass. That is the right shape for this
claim.

## Claim: rider-cancels-before-start
Verdict: sound
Fingerprint: da2a764ee8c3fdba
Judged: 2026-08-07
Judge: claude-opus-5

Two tests again. `Cancellation_is_permitted_from_every_non_terminal_state` derives the non-terminal
set and asserts cancellation is permitted from each and lands in `cancelled` — universal over the
states, which is the axis "before completion" names.

`Cancellation_records_the_cancelling_party_and_is_refused_after_completion` carries the attribution
half over HTTP: it cancels as `rider` and asserts the last history entry names `rider`. Against a
handler that recorded a constant actor, the driver case in the same test fails.

## Claim: driver-cancels-after-assignment
Verdict: sound
Fingerprint: 9dac8d8486ad4df3
Judged: 2026-08-07
Judge: claude-opus-5

The mirror of the above, and the reason the attribution assertion is not vacuous: the same test
cancels one trip as `rider` and another as `driver-0`, asserting each is recorded as itself. One
case alone would pass against a hard-coded actor; the pair does not.

## Claim: cancellation-after-completion-rejected
Verdict: sound
Fingerprint: e9b0e838e4ab2365
Judged: 2026-08-07
Judge: claude-opus-5

Covered twice. `A_terminal_state_admits_no_event_at_all` includes `(Completed, Cancel)` in its
derived sweep. The component test drives a trip to completion over HTTP and asserts the cancellation
is refused with `trip:trip:transition:not_permitted` — the code a client branches on, not merely a
non-200.

Against a handler that treated cancellation as always allowed, both fail.
