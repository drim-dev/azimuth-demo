# Judgments: trips/dispatch

Re-judged 2026-08-10 after D28 exposed realization sources. `OfferTripToDrivers` owns eligibility
filtering and offer creation, `AcceptOffer` owns the assignment compare-and-set and withdrawal, and
`GetOffers` owns expiry transition and the observable offer set. Those responsibilities establish
an identifiable part of every relation attached to them.

Revalidated 2026-08-10 after D27 added stable mechanism ids. The semantic diff changed no
enforcement, binding, expectation, rationale, claim, evidence form or source; the prior verdict
rationales therefore remain applicable and only their freshness fingerprints moved.

Re-judged 2026-08-08 after assignment began writing a versioned lifecycle event. The conditional
assignment, offer outcomes and refusal assertions remain unchanged; the winning transaction now
also increments the aggregate version and commits one outbox event, while losing transactions write
neither.

Rebased 2026-08-08 after criticality entered claim freshness. No level, evidence site, required
form or verdict changed.

Re-judged 2026-08-08 after the bound acceptance handler entered the freshness fingerprint. Dispatch
now asks `TripStateMachine.Next` for the assignment target instead of duplicating `Assigned`; the
contention and late-arrival evidence remains discriminating.

First pass, 2026-08-07. Four tags were corrected before judging and one test was strengthened; both
changes are described in the entries that rest on them.

**Conflict of interest:** the judge did not write these tests, but wrote the retagging and the
strengthening of `late-acceptance-rejected`.

## Claim: offer-sent-to-available-nearby-driver
Verdict: sound
Fingerprint: 7bef9855798b245a
Judged: 2026-08-08
Judge: codex

`Only_available_nearby_drivers_are_offered` seeds a population chosen to separate the two conditions
— three available and near, two unavailable, one available but in another area — and asserts the
offered set is *exactly* the three.

The exactness is what gives it teeth. Against a dispatcher that offered to everyone, the equivalence
assertion fails; against one that filtered on availability but not proximity, `driver-far` appears
and it fails; against one that offered to nobody, it fails. Three distinct wrong implementations,
three distinct failures.

Retagged `Example`: the population is hand-built, so this is exhaustive over one population rather
than universal over populations. `standard`'s floor is `example`.

## Claim: unavailable-driver-not-offered
Verdict: sound
Fingerprint: fa3a371e66723b57
Judged: 2026-08-08
Judge: codex

The negative half of the same test, and it is asserted twice over — once by the exact-set
equivalence and once by explicit `NotContain` assertions for both unavailable drivers. Redundant,
and cheaply so: the second form says out loud what the first implies.

## Claim: no-available-drivers
Verdict: sound
Fingerprint: 6815352259c22fbd
Judged: 2026-08-08
Judge: codex

`No_available_drivers_means_no_offers` seeds only an unavailable driver and a distant one, then
asserts both that the offers endpoint returns empty *and* that the offers table holds zero rows.

The second assertion is the one that matters: a dispatcher that wrote offers and filtered them on
read would pass the first and fail the second. That distinction is the difference between "no offer
is made" and "no offer is shown", and the claim says the former.

Recorded gap: the scenario's second line — "the rider is told no driver is available" — is not
asserted anywhere. `RequestRide.Response` carries `DriversOffered`, so a zero is observable, but no
test reads it for this case. Not enough to fail the claim, since the primary outcome is checked
against the store, but it is the half a rider actually experiences.

## Claim: first-acceptance-assigns
Verdict: sound
Fingerprint: f2ef7d418bcf83e9
Judged: 2026-08-10
Judge: codex

`Exactly_one_driver_is_assigned_however_many_accept_together` fires six acceptances at once, five
trials, resetting between them, and asserts exactly one returns OK, the trip is stored as
`assigned`, and an assigned driver id is present.

That last pair is what covers this claim rather than only its sibling: an implementation that
refused everyone would satisfy "at most one" and fail here. `Universal` is honest — the axis is how
many accept and the test ranges over it.

## Claim: concurrent-acceptances-yield-one-assignment
Verdict: sound
Fingerprint: 4225f17a0dbaab7a
Judged: 2026-08-10
Judge: codex

The same test read as the claim states it: *any number of drivers accepting concurrently* yields
exactly one assignment, and every other accepter is told the offer was taken. Both are asserted —
the winner count and the refusal code on the losers.

Against an implementation relying on a read-then-write check with no constraint behind it, the race
assigns more than once and the count fails. Five trials is thin for a race, as elsewhere in this
corpus, and is stated rather than hidden.

## Claim: late-acceptance-rejected
Verdict: sound
Fingerprint: 89a25882f2985c05
Judged: 2026-08-10
Judge: codex

The claim says *any further driver*, and the test now ranges over that. It seeds five drivers, lets
one accept, then has every other driver attempt acceptance twice, asserting each is refused with
`trip:dispatch:accept:offer_taken` and that the stored assignment is unchanged at the end.

The second round is deliberate: an implementation that reassigned on a losing acceptance would leave
the first round looking correct and be caught by the second. Before the change this was one late
acceptance by one driver — an example under a `universal` tag, which is the pattern this corpus has
produced repeatedly.

## Claim: other-offers-withdrawn
Verdict: sound
Fingerprint: dbb11f89063894dd
Judged: 2026-08-08
Judge: codex

`Assignment_withdraws_every_other_offer` seeds four drivers, accepts as one, and asserts the
accepter's offer reads `accepted`, that *every* other offer reads `withdrawn`, and that the store
holds exactly one accepted row.

`OnlyContain` over the others is the right shape: it fails if any single offer is left standing,
which is what a partial withdrawal looks like. Retagged `Example` — one population, one accepter.

## Claim: expired-offer-withdrawn
Verdict: sound
Fingerprint: 4fefcce1ac43fcbf
Judged: 2026-08-08
Judge: codex

`An_offer_past_its_expiry_is_withdrawn` asserts the offers stand while fresh, advances the clock a
minute past the thirty-second window, and asserts they are all withdrawn — and, importantly, that
the collection is not empty, so a dispatcher that deleted expired offers rather than withdrawing
them fails rather than passing vacuously.

Already tagged `Example` before this pass, with a plan entry that lowers the requirement and records
why: expiry is checked at a single boundary instant, not across the range of clock skew between the
driver client and the service, accepted because clock-skew handling is not yet designed. That is the
refusal path used correctly, and it is the reason this claim needs no correction.
