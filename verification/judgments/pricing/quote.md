# Judgments: pricing/quote

First pass, 2026-08-07. Six tags were corrected before judging: each declared `universal` over one
scripted case, and every claim's floor permits `example`, so the honest tag costs nothing. Judging
them as they stood would have produced six `dishonest-tag` verdicts for a fault that is a word.

**Conflict of interest:** the judge did not write these tests, but did write the retagging and has
written most of what surrounds them.

**Fingerprints refreshed 2026-08-07.** Every verdict below was re-affirmed rather than re-derived:
the evidence files changed for reasons belonging to other specs — tag corrections in shared test
files — and no test body carrying these claims was touched. The fingerprint expired because it
hashes whole files, which D19.1 records.

## Claim: quote-returned
Verdict: sound
Fingerprint: feab0d3db94af130
Judged: 2026-08-07
Judge: claude-opus-5

`An_issued_quote_carries_a_total_a_currency_and_an_expiry` asserts the identifier is non-empty, the
total and currency are present and correct for the input, the expiry is the issuance instant plus
the validity window, and a row exists in the store carrying the same currency.

The claim is about what a quote *carries*, not about how the total is computed — that is
`total-equals-components`, judged separately — so a literal expectation is adequate here. Against an
issuer that returned no identifier, the assertion fails and every downstream test that references a
quote fails with it. Against one that omitted the expiry, the instant assertion fails.

Tagged `Example`, which is what one pickup-and-dropoff pair is, and `standard`'s floor is `example`.

## Claim: unserviceable-area
Verdict: sound
Fingerprint: ce47098746217f06
Judged: 2026-08-07
Judge: claude-opus-5

`e2e.test.ts:206` posts a quote request with an empty pickup through the rider app and asserts a 400
carrying `pricing:quote:issue:unserviceable_area`. The plan puts this at `e2e` because the refusal
is a thing a rider observes, and the evidence is at that scope.

Against a validator that accepted the request, the status assertion fails; against one that refused
with a different code, the second assertion fails — and the code, not the message, is what a client
branches on.

Recorded: an empty pickup *stands in for* an unserviceable area. `IssueQuote`'s own comment says so.
The claim says "outside every serviced market" and the fixture has no notion of markets, so what is
verified is the refusal mechanism rather than the rule. That is a fixture limitation and not a
defect in this evidence, but a reader should not take this claim as covering real serviceability.

## Claim: quote-valid-before-expiry
Verdict: sound
Fingerprint: ef93f1225f470225
Judged: 2026-08-07
Judge: claude-opus-5

`A_quote_is_valid_until_its_expiry_and_not_after` reads a fresh quote and asserts it is not expired,
then advances the clock past the validity window and asserts it is. Both halves are in one test,
which is right: an implementation that reported everything valid passes the first assertion and
fails the second.

## Claim: quote-invalid-after-expiry
Verdict: sound
Fingerprint: fdd071794e24bd11
Judged: 2026-08-07
Judge: claude-opus-5

Same test, and it carries the scenario's second half explicitly: after expiry the total is asserted
*unchanged*. That matters more than it looks — an implementation that zeroed or recomputed the total
on expiry would satisfy "reported expired" and violate the claim as written.

The offset is one second past the boundary, one point. Tagged `Example` and it is one; the near side
is covered by the sibling above, so the boundary itself is bracketed even though neither test ranges
over the offset.

## Claim: expired-quote-is-never-revalidated
Verdict: sound
Fingerprint: f854f05f9906d6c7
Judged: 2026-08-07
Judge: claude-opus-5

`An_expired_quote_stays_expired_and_a_new_one_gets_a_new_identity` expires a quote, issues a fresh
one, asserts the identifiers differ and the new one is valid, then advances another hour and asserts
the old one is *still* expired.

The last step is what gives it teeth. Against an implementation that derived validity from the most
recent issuance, or that reset expiry on read, the final assertion fails. The test's own comment
names the mechanism — expiry is derived on read rather than written by a sweeper, so there is no
path that moves it back — and the assertion checks the consequence rather than restating the
comment.

## Claim: total-in-minor-units
Verdict: sound
Fingerprint: 8c9eb42cbabd9afb
Judged: 2026-08-07
Judge: claude-opus-5

The plan records proof strength here with a stated mechanism: money is an integer-backed type with
no floating-point constructor, conversion, or arithmetic operator, so a non-integral amount is
unrepresentable rather than untested. `design/pricing/quote.md` carries the matching `Enforcement:
type`, so this is not an unbacked proof.

`An_amount_states_its_currency_and_counts_minor_units` is supplementary and tagged `Example`, which
it is. It asserts the currency is normalized and that a blank currency throws.

Worth recording because it is the corpus's clearest case of a claim that needs no test: what makes
it sound is that `Money` has no constructor that could violate it, and a runtime test could only
sample what the compiler already forbids. The design entry is also the one place in the repo where a
type-level claim was checked against the code and *corrected* — an earlier version claimed
`Money.Sum` refused mixed currencies at the type level, which it cannot without phantom types.

## Claim: total-equals-components
Verdict: sound
Fingerprint: 1b59751eab9281d6
Judged: 2026-08-07
Judge: claude-opus-5

The strongest evidence in the corpus, and the only claim carrying a metamorphic oracle.

`A_total_equals_the_sum_of_its_components` generates 500 component sets of varying size, including
empty ones and negative amounts, and asserts two things: that summing the whole equals summing two
halves and adding them, and that the total equals the independent sum. The first is metamorphic — it
compares the implementation against itself under a transformation, so it does not depend on anyone
recomputing the arithmetic correctly in the test.

Against an implementation that dropped the last component, the split assertion fails for odd counts
and the direct assertion fails for all. Against one that saturated or wrapped on overflow, the
negative amounts and the split disagree. `Universal` is honest: the axis is the component set and
the test ranges over it.

## Claim: breakdown-accompanies-quote
Verdict: sound
Fingerprint: 1d1ee4d4934513fd
Judged: 2026-08-07
Judge: claude-opus-5

`An_issued_quote_carries_the_components_that_make_up_its_total` asserts the labels are exactly
`base` and `distance` and that the component amounts sum to the quote's total.

The second assertion is the one with teeth: a breakdown that omitted a component, or listed one
twice, fails it. Against a breakdown returned as an empty list, both fail.

`routine` requires no evidence at all (`Strength: none`), so this claim owed nothing and has a
discriminating test anyway. Tagged `Example`, which it is.
