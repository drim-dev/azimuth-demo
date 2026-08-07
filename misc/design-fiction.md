# The mechanism facet can describe code nobody wrote

**Status: finding, measured. One rubric change already applied.** From judging the whole corpus on
2026-08-07.

## What was found

Three entries in `design/` named mechanisms that do not exist:

| Entry | Claimed | Actual |
|---|---|---|
| `trips/rider-view#driver-hidden-after-terminal` | `RiderTripStream.Close`, a choke point invoked by the terminal transition | no such type; the fixture has no streaming, the page polls `router.refresh()` |
| `trips/rider-view` residue | the rider client caches the last known position, leaving a cold-start window | `trip-service.ts` fetches `cache: 'no-store'`; `refresher.tsx` exists to avoid client merging |
| `payments/capture#capture-on-completion` | `CompleteTrip` writes a capture-intent row in the same transaction | the trip service has no reference to payments, no intent table, no outbox |

All three read as confident mechanism descriptions. All three survived every check the repo has.

## Why nothing catches it

`Enforcement:` is a closed vocabulary and is parsed. **`Site:` is free prose and is not.** The
design facet's checks are about presence and consistency of *entries* — `dangling-design-entry`
fires for an entry naming a requirement that does not exist, `undeclared-mechanism` for a critical
requirement with no entry, `unbacked-proof` for proof-strength evidence with no top-rung mechanism
behind it. None of them opens the file the `Site:` names.

So the mechanism facet is the only one whose content is unverifiable by construction. Specs are
prose too, but a spec is a claim about intent — it cannot be false about the code, only unmet.
A design entry asserts that a particular thing exists, which is a checkable proposition nobody
checks.

## The worse half: the agent tier trusted it

Two `spec-gap` verdicts in `trips/rider-view` were reached by citing the phantom stream as evidence
that the spec was silent about a pushed observation mode. The judge read design prose as fact.

`azimuth-verify` told the judge to read test *bodies* rather than test names, and said nothing about
the design. That is now fixed — step 2b requires opening the file a `Site:` names — but the general
shape is worth stating: **a rubric that says "verify one artifact against the source" implies the
others need no verifying, and the implication is wrong.**

## Options for making it checkable

1. **Nothing.** Accept that `Site:` is prose and rely on the agent tier, which now has the step.
2. **Symbol-shaped sites.** Require the identifier part of a `Site:` to resolve against the
   extractor's symbol table, warning when it does not. Cheap for `Type.Member` shapes, useless for
   the prose half of an entry, and the prose half is where most of the content is.
3. **Realizes-backed sites.** Require that a claim's design site be, or contain, a site that carries
   a `Realizes` tag for that requirement. Derived rather than parsed, and it would have caught all
   three: none of the phantom sites had a tag, because none of them existed.

Option 3 is the interesting one and is not proposed here — it wants evidence from a second corpus
that design entries and realizing sites line up often enough for a mismatch to mean something.

## What it does for the framework's central claim

D3's bet is that the mechanism facet is load-bearing and its absence is why the other two rot. This
session gives that bet its first real test and the result is mixed: the design facet **did** carry
information the other two lacked — it is what made the two `spec-gap` verdicts look reasonable, and
what named the storage constraint that settled a required scope — and it was also **wrong three
times in a way nothing could detect**.

An artifact that is load-bearing and unverifiable is not an argument against having it. It is an
argument that it needs the same treatment the other two got: something derived, checking it.
