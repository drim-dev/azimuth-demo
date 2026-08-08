# Judgments: trips/driver-view

Rebased 2026-08-08 after criticality entered claim freshness. No level, evidence site, required
form or verdict changed.

Re-judged 2026-08-08 after design bindings and their source entered the freshness fingerprint.
`RiderContact.Reveal()` disproved the earlier type/proof rationale. The plan now declares the e2e as
an example with an accepted egress-analysis residual, and the projection guards were read directly.

Fingerprints refreshed 2026-08-08 after the shared e2e file gained pricing and payment assertions.
The five driver tests and their production projections were re-read and the full e2e suite passed;
none of the driver paths changed, so the existing sound rationales remain applicable.

First pass, 2026-08-07. Five e2e tags were corrected before judging — each declared `universal` over
one scripted path — and two plan entries were added so that the two `critical` claims are carried by
the mechanism that actually holds them rather than by an over-declared tag.

**Conflict of interest:** the judge did not write these tests, but wrote the retagging and both plan
entries.

## Claim: rider-contact-confined-to-held-trips
Verdict: sound
Fingerprint: b8b8c3e11732d0ef
Judged: 2026-08-08
Judge: codex

The driver-side invariant, and the class is now derived: `invariant-breach` takes membership from
the built route table of the driver app, so a route is a member because it exists. That found three
driver surfaces outside the class — both API routes and the home page — none of which any
tag-derived enumeration could reach. All three are discharged with stated reasons.

The e2e is tagged `example`, which is what one held trip and one other driver are, and the plan
already lowered the requirement to `example` with an accepted residual before this pass — that the
class is checked structurally and no test enumerates every driver-facing surface. That residual is
now less true than when it was written, which is the right direction.

What gives the behavioural half teeth is the third assertion: a *second* driver reads the same trip
while it is live and gets `null`. Against a projection that revealed the contact to any driver once
the trip was held by someone, the first two assertions pass and that one fails.

## Claim: pickup-shown-on-offer
Verdict: sound
Fingerprint: bf93505a8ee701bc
Judged: 2026-08-08
Judge: codex

`an offer shows the pickup and no rider` asserts the pickup area is `downtown` and that the fare is
present as a number, through the driver app's API and again in the rendered page.

Against a projection that omitted the pickup — the failure that makes an offer useless — the first
assertion fails. The fare check is weaker: `typeof … === 'number'` passes for any number, so an
implementation quoting the wrong fare is not caught here. It is caught by
`pricing/quote#total-equals-components` at the point the fare is computed, which is where that
belongs.

The plan now lowers this to `example` with a recorded reason: a type can carry the *hiding* claims
and cannot carry a positive one, and ranging over offer shapes would exercise the fixture's single
market repeatedly. That is the deviation path used deliberately rather than an over-declared tag.

## Claim: rider-contact-hidden-on-offer
Verdict: sound
Fingerprint: 7f4213e987aa4b59
Judged: 2026-08-08
Judge: codex

The e2e asserts the rider's identifier appears nowhere in the offer payload and that the string
`proxy` appears nowhere either — a substring check over the whole serialized body, so a contact
leaking through a differently-named field is still caught. It repeats the check against the rendered
offer page.

The tag is honestly `example`, and the plan now accepts that scope rather than claiming proof.
`DriverProjection.Offer` has no contact field and the derived route class forces future surfaces to
declare a discharge, but neither fact makes a false declaration impossible.

## Claim: proxy-contact-while-held
Verdict: sound
Fingerprint: f27c30ba23bfeb90
Judged: 2026-08-08
Judge: codex

The positive half: once the driver accepts, the contact appears, and the test asserts its exact
value rather than merely that something non-null arrived.

Recorded, and it matters for how this claim should be read: `design/trips/driver-view.md` states
that proxy contacts are **not implemented** — the projection returns a placeholder token where a
real system would mint a per-trip number that expires. The design says so plainly and calls it the
honest state. So what is verified is that a contact is revealed only while held, not that it is a
proxy in any sense that protects a rider's real number. The claim's wording survives that because a
placeholder satisfies "a proxy contact"; a reader should know the mechanism is a stand-in.

This is the one place in this spec where the design's residue is doing the work a claim would
otherwise have to.

## Claim: contact-withdrawn-after-terminal
Verdict: sound
Fingerprint: f594cee0b137a9db
Judged: 2026-08-08
Judge: codex

After completion the same driver reads the same trip and gets `null`, and the whole payload is
checked for the rider's identifier as well as the contact field.

Against a projection that keyed the reveal on "this driver was ever assigned" rather than on the
trip being live, every earlier assertion passes and this one fails — which is the natural way to
write the bug, since the assignment is still recorded after completion.

The plan raises this to `e2e` for composition risk and the evidence is at that scope, through two
processes and a real store.
