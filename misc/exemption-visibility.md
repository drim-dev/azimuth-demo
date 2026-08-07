# Exemptions are recorded in source and invisible to the model

**Status: one finding, one candidate spec change, one lifecycle question answered.** Found on
2026-08-07 while asking why the validator tests in `RequestRideTests.cs` carry `[Untraced]`.

## The finding

`UntracedAttribute` takes a reason and documents it as "recorded for review" (D6.3: a deliberate,
attributable, reviewable exemption is fine anywhere; a silent absence is not). Two things then drop
it before it reaches anything:

- `Collector.Untraced` is `record Untraced(string Site, string File)`. **There is no reason field.**
- An exempt test is omitted from the manifest entirely — `Collector.cs:155` adds to
  `untraced_tests` only when a test is traced, uncovered **and not exempt**, which is the hole list.
  So `untraced_tests: 0` is the healthy state, and nothing records that an exemption happened.

There are **17 exemptions in the corpus** — 16 `[Untraced]` in `app/services` across six files, and
one `untraced(…)` in `app/e2e/src/e2e.test.ts` — and not one of them appears in
`.azimuth/dotnet.json`, `.azimuth/web.json`, the derived model, or the export.

**Why it matters.** D6.3's whole argument is that the exemption is what makes "let the team decide
the degree of rigor" honest rather than corrosive. Compare the evidence-side equivalent: lowering a
required form needs `Residual:` and `Accepted:`, which are parsed, checked (`check.rs:381`), and
visible in the plan. The test-side equivalent is a string in a C# attribute that no artifact reads.
Reviewing exemptions today means grepping source, which is the one place this framework otherwise
refuses to leave a judgment.

It is also the one exemption mechanism with no ratchet. D8's ratchet works on counts in the model;
a category the model cannot see cannot be ratcheted, so the exemption count can only be governed by
someone remembering to look.

## Options

1. **Emit exempt tests with their reason**, in a separate array from `untraced_tests` so the hole
   list keeps its meaning. Small change to both extractors and the manifest schema; makes the count
   reviewable and ratchetable.
2. **Export-only.** Carry exemptions into the D10 export without giving them a hole kind. Cheaper,
   and enough for review, but still not ratchetable.
3. **Nothing.** Defensible if nobody ever needs to review an exemption — but then the reason string
   is decoration, and `UntracedAttribute`'s documentation overstates what it does.

Not ranked, because the question below has not been asked of anyone.

**What would settle it:** whether an exemption has ever been wrong. Seventeen exist; none has been
reviewed by anything but the author who wrote it, and there is no artifact that would let a reviewer
find them.

## Related — an unspecified boundary the exemptions are standing in for

The validator tests are exempt for a good reason: `specs/trips/request.md` owns no claim about
request *shape*. Its three requirements are about quotes, trips and active-trip uniqueness. Tagging
`A_request_names_its_rider` to `request-admitted-with-valid-quote` would be a false linkage.

But the absence of that claim is itself visible from outside. The validator draws a client-facing
line the spec does not mention:

| Input | Status | `errorCode` |
|---|---|---|
| empty `quoteId` | 400 | `validation:request:validate:invalid` |
| non-empty, undecodable (`"not-an-id"`) | 422 | `trip:request:create:unknown_quote` |
| well-formed, absent | 422 | `trip:request:create:unknown_quote` |

A client branches on those codes. Found while writing
`An_unrecognised_quote_is_refused_whatever_identifier_is_offered`, which deliberately excludes the
empty string from its range because it behaves differently — the test knows about a boundary the
spec does not state.

**Candidate spec change, not confirmed:** a scenario in `specs/trips/request.md` fixing which
malformed inputs are refused as ill-formed and which as unknown. It is the same shape as the two
`spec-gap` verdicts in `trips/rider-view` — code right, tests fine, reader surprised — but it has
not been through a judging pass and should not be treated as a finding until it has. The honest
objection is that request well-formedness may belong to a transport concern that no domain spec
should own.

## If those rules land in a spec, do the `[Untraced]` markers come off?

**It depends what the claim says, and the answer is not automatic.**

- If a new claim is about **what the rider is shown** — status and error code over HTTP — then these
  three tests still do not cover it. They exercise `RequestRide.RequestValidator` directly, not the
  client-visible refusal, and the honest evidence is the component test that already ranges over
  identifiers. The exemptions stay, and their reason strings stay true.
- If a claim is about **the validator rules themselves**, the tests become its covering evidence,
  `[Covers]` replaces `[Untraced]`, and the exemption must come off — leaving it would be a tag
  asserting "this test legitimately covers no claim" while it covers one. Mechanically both
  attributes can coexist (`covered` and `exempt` are independent flags), so nothing forces the
  removal; it is the same honesty rule that governs every other tag.

**The failure mode in between is worth naming.** Add the claim, forget the tests: the claim reports
`uncovered` — loud, so not silent — while the test that would cover it sits exempt beside it.
Someone then writes a second test for a rule already tested. `uncovered` fires, so the model is not
wrong; but an exempt test in the same slice as an uncovered claim is a pairing a check could notice,
and cannot today, because exemptions are not in the model. That is the finding above, arriving from
the other end.
