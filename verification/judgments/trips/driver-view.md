# Judgments: trips/driver-view

First pass, 2026-08-07. Five e2e tags were corrected before judging — each declared `universal` over
one scripted path — and two plan entries were added so that the two `critical` claims are carried by
the mechanism that actually holds them rather than by an over-declared tag.

**Conflict of interest:** the judge did not write these tests, but wrote the retagging and both plan
entries.

## Claim: rider-contact-confined-to-held-trips
Verdict: sound
Fingerprint: 84900865dd778dab
Judged: 2026-08-07
Judge: claude-opus-5

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
Fingerprint: 3af62e02b5692777
Judged: 2026-08-07
Judge: claude-opus-5

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
Fingerprint: 429ae531392b0572
Judged: 2026-08-07
Judge: claude-opus-5

Two independent things hold this claim, which is why it survives an `example` tag on its test.

The e2e asserts the rider's identifier appears nowhere in the offer payload and that the string
`proxy` appears nowhere either — a substring check over the whole serialized body, so a contact
leaking through a differently-named field is still caught. It repeats the check against the rendered
offer page.

The plan now records the mechanism as proof-strength evidence: `RiderContact` has no serializer and
no raw accessor, and `DriverProjection.For(held)` is its only reveal. `design/trips/driver-view.md`
carries the matching `Enforcement: type`, so this is not proof claimed out of thin air, and a future
driver-facing route cannot reach past it by accident.

## Claim: proxy-contact-while-held
Verdict: sound
Fingerprint: 472ed318f13121f6
Judged: 2026-08-07
Judge: claude-opus-5

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
Fingerprint: 51d7d0136f8d6493
Judged: 2026-08-07
Judge: claude-opus-5

After completion the same driver reads the same trip and gets `null`, and the whole payload is
checked for the rider's identifier as well as the contact field.

Against a projection that keyed the reveal on "this driver was ever assigned" rather than on the
trip being live, every earlier assertion passes and this one fails — which is the natural way to
write the bug, since the assignment is still recorded after completion.

The plan raises this to `e2e` for composition risk and the evidence is at that scope, through two
processes and a real store.
