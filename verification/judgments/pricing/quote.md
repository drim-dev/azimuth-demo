# Judgments: pricing/quote

Re-judged 2026-08-08 after design bindings and bound source entered the freshness fingerprint.
`Money`, both token codec directions and the sole quote-construction handler were read directly;
the existing verdict rationales remain applicable.

Re-judged for `market-aware-surge-quotes`. The routine breakdown claim is intentionally absent:
D20 gives routine claims no agent-tier obligation, and retaining its old judgment would make a
stale optional artifact look like a framework finding.

## Claim: quote-returned
Verdict: sound
Fingerprint: ee48bf6313ae6010
Judged: 2026-08-08
Judge: codex

`Every_serialized_quote_total_is_the_sum_of_its_three_components` also asserts a non-empty public id
and token, an expiry after issuance, and the requested currency on the HTTP response. Its tag is
`example`, not `universal`; the loop supplies useful variation but does not overstate the claim.
Removing any required response field makes the test fail during assertion or deserialization.

## Claim: unserviceable-area
Verdict: sound
Fingerprint: cbf16aa279411543
Judged: 2026-08-08
Judge: codex

The e2e submits pickup `outside` through the rider BFF and asserts both 400 and the stable
`unserviceable_area` code. `IssueQuote.RequestValidator` was checked against the source: it admits
only the fixture's `downtown` market. Accepting every non-empty pickup or changing the refusal code
fails this evidence.

## Claim: quote-valid-before-expiry
Verdict: sound
Fingerprint: d44ba1d05c856200
Judged: 2026-08-08
Judge: codex

The component test reads a fresh quote and again one tick before expiry against real storage and an
injected clock. Expiring immediately or one tick early fails; the sibling assertion at the exact
instant prevents a permanently-valid implementation from passing the near side alone.

## Claim: quote-invalid-after-expiry
Verdict: sound
Fingerprint: 456e7fdce7add6ec
Judged: 2026-08-08
Judge: codex

At exactly the expiry instant the same stored quote is reported expired and its total is asserted
unchanged. A grace period, strict-`<` boundary or zeroing/recalculation on expiry fails.

## Claim: expired-quote-is-never-revalidated
Verdict: sound
Fingerprint: 0c5fe4bcae680702
Judged: 2026-08-08
Judge: codex

After expiry the test issues a second quote, proves its identity differs and that it is live, then
reads the original again and proves it remains expired. Reusing an id or deriving old validity from
the latest issuance fails.

## Claim: total-in-minor-units
Verdict: sound
Fingerprint: f1516e6299e55c21
Judged: 2026-08-08
Judge: codex

The design site exists: `Money` exposes only `long MinorUnits`, has no floating-point constructor or
conversion, and normalizes an explicit currency. The plan correctly limits proof to the .NET
boundary and records JavaScript precision as residue. The example test is supplementary rather than
being presented as the proof.

## Claim: total-equals-components
Verdict: sound
Fingerprint: acacf230958068d4
Judged: 2026-08-08
Judge: codex

One test generates 500 variable-length component sets and checks both an independent sum and a
split/recombine metamorphic relation. The Pricing component test adds real HTTP serialization,
three currencies and both surge branches, then decodes the token. Dropping a component, returning a
constant, or serializing a different total fails at least one independent relation. The
`QuoteTokenCodec` design site also exists and rejects mismatched totals on both encode and decode.

## Claim: current-pressure-selects-surge
Verdict: sound
Fingerprint: f3e67b1e5b38a248
Judged: 2026-08-08
Judge: codex

The component evidence covers the policy's complete relation partition: below, equal to and above
available supply, including zero and large counts. Its expected amount is an independent integer
expression. Changing `>` to `>=`, always applying surge, returning a constant positive value or
removing surge fails. The e2e additionally proves a fresh observation reaches the process that
issues the quote.

## Claim: stale-pressure-does-not-select-surge
Verdict: sound
Fingerprint: fe8b97d103d98906
Judged: 2026-08-08
Judge: codex

Against a real stored high-pressure observation, the test proves positive surge one tick before the
five-minute boundary and zero surge exactly at it. A hidden grace period, inclusive stale boundary
or policy that ignores observation age fails. The source confirms `IssueQuote` selects only rows
strictly newer than the boundary.

## Claim: surge-is-a-quote-component
Verdict: sound
Fingerprint: 8f8367b32fbb0ad5
Judged: 2026-08-08
Judge: codex

The component evidence asserts the exact ordered labels `base`, `distance`, `surge` over currencies,
distances and both pressure branches, checks their serialized sum, and decodes the signed token. The
e2e requires a positive surge and carries that same token through Trips to the captured amount.
Omitting, relabelling or excluding surge from either signed total or capture makes one of those
relations fail.
