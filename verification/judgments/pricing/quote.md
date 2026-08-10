# Judgments: pricing/quote

Revalidated 2026-08-10 after token integrity became a reusable security concern and canonical
base64url decoding was enforced. Every stale covering body and bound source was re-read. The new
decoder check only narrows accepted encodings; component-sum validation, money representation and
the pressure policy are unchanged, so the existing soundness rationales still apply.

Revalidated 2026-08-10 after D27 added stable mechanism ids. The semantic diff changed no
enforcement, binding, expectation, rationale, claim, evidence form or source; the prior verdict
rationales therefore remain applicable and only their freshness fingerprints moved.

Re-read 2026-08-08 after the composed surge path acquired a broker and Analytics consumer. The
Pricing component bodies did not change; the e2e still carries one positive surge unchanged through
the signed quote and now through the lifecycle-event handoff before capture.

Rebased 2026-08-08 after criticality entered claim freshness. No level, evidence site, required
form or verdict changed.

Re-judged 2026-08-08 after design bindings and bound source entered the freshness fingerprint.
`Money`, both token codec directions and the sole quote-construction handler were read directly;
the existing verdict rationales remain applicable.

Re-judged for `market-aware-surge-quotes`. The routine breakdown claim is intentionally absent:
D20 gives routine claims no agent-tier obligation, and retaining its old judgment would make a
stale optional artifact look like a framework finding.

## Claim: quote-returned
Verdict: sound
Fingerprint: da9e540896c7c183
Judged: 2026-08-08
Judge: codex

`Every_serialized_quote_total_is_the_sum_of_its_three_components` also asserts a non-empty public id
and token, an expiry after issuance, and the requested currency on the HTTP response. Its tag is
`example`, not `universal`; the loop supplies useful variation but does not overstate the claim.
Removing any required response field makes the test fail during assertion or deserialization.

## Claim: unserviceable-area
Verdict: sound
Fingerprint: ba9f4308e7bff274
Judged: 2026-08-08
Judge: codex

The e2e submits pickup `outside` through the rider BFF and asserts both 400 and the stable
`unserviceable_area` code. `IssueQuote.RequestValidator` was checked against the source: it admits
only the fixture's `downtown` market. Accepting every non-empty pickup or changing the refusal code
fails this evidence.

## Claim: quote-valid-before-expiry
Verdict: sound
Fingerprint: 52faaafbaa4121ef
Judged: 2026-08-08
Judge: codex

The component test reads a fresh quote and again one tick before expiry against real storage and an
injected clock. Expiring immediately or one tick early fails; the sibling assertion at the exact
instant prevents a permanently-valid implementation from passing the near side alone.

## Claim: quote-invalid-after-expiry
Verdict: sound
Fingerprint: 850de0dc3db1a09f
Judged: 2026-08-08
Judge: codex

At exactly the expiry instant the same stored quote is reported expired and its total is asserted
unchanged. A grace period, strict-`<` boundary or zeroing/recalculation on expiry fails.

## Claim: expired-quote-is-never-revalidated
Verdict: sound
Fingerprint: ab11ae8bc8036c51
Judged: 2026-08-08
Judge: codex

After expiry the test issues a second quote, proves its identity differs and that it is live, then
reads the original again and proves it remains expired. Reusing an id or deriving old validity from
the latest issuance fails.

## Claim: total-in-minor-units
Verdict: sound
Fingerprint: e2de40c9a3e17df7
Judged: 2026-08-10
Judge: codex

The design site exists: `Money` exposes only `long MinorUnits`, has no floating-point constructor or
conversion, and normalizes an explicit currency. The plan correctly limits proof to the .NET
boundary and records JavaScript precision as residue. The example test is supplementary rather than
being presented as the proof.

## Claim: total-equals-components
Verdict: sound
Fingerprint: 9c8490acf3f98fcc
Judged: 2026-08-10
Judge: codex

One test generates 500 variable-length component sets and checks both an independent sum and a
split/recombine metamorphic relation. The Pricing component test adds real HTTP serialization,
three currencies and both surge branches, then decodes the token. Dropping a component, returning a
constant, or serializing a different total fails at least one independent relation. The
`QuoteTokenCodec` design site also exists and rejects mismatched totals on both encode and decode.

## Claim: current-pressure-selects-surge
Verdict: sound
Fingerprint: 8e0396226f329454
Judged: 2026-08-10
Judge: codex

The component evidence covers the policy's complete relation partition: below, equal to and above
available supply, including zero and large counts. Its expected amount is an independent integer
expression. Changing `>` to `>=`, always applying surge, returning a constant positive value or
removing surge fails. The e2e additionally proves a fresh observation reaches the process that
issues the quote.

## Claim: stale-pressure-does-not-select-surge
Verdict: sound
Fingerprint: c44c476f6af551ba
Judged: 2026-08-10
Judge: codex

Against a real stored high-pressure observation, the test proves positive surge one tick before the
five-minute boundary and zero surge exactly at it. A hidden grace period, inclusive stale boundary
or policy that ignores observation age fails. The source confirms `IssueQuote` selects only rows
strictly newer than the boundary.

## Claim: surge-is-a-quote-component
Verdict: sound
Fingerprint: d755e6e012d7bcfe
Judged: 2026-08-10
Judge: codex

The component evidence asserts the exact ordered labels `base`, `distance`, `surge` over currencies,
distances and both pressure branches, checks their serialized sum, and decodes the signed token. The
e2e requires a positive surge and carries that same token through Trips to the captured amount.
Omitting, relabelling or excluding surge from either signed total or capture makes one of those
relations fail.
