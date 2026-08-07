# Verification: trips/request

## Claim: quote-consumed-once
Scope: component
Quantification: universal

Single consumption is a uniqueness claim over concurrent requests, settled by storage. The
sequential version of this test passes against an implementation with no constraint.

## Claim: second-request-rejected-while-active
Scope: component
Quantification: universal

"A rider holds at most one active trip" is the same shape as `captured-once`: two requests
arriving together are the case that matters, and they are only distinguishable against a real
store.

## Claim: request-rejected-with-unknown-quote

*(revised 2026-08-07 — supersedes `Oracle: contract`)* No deviation; the standard applies.

The superseded entry read: "the rejection depends on how `pricing/quote` answers a lookup for an
identifier it does not recognise… the failure mode is a disagreement between two services about what
'unknown' looks like". There are no two services. Quotes are issued by the trip service's own
`/quotes` slice into `TripDbContext.Quotes`, and `Pricing` is a library — `Money.cs`. The lookup is
a local read.

Kept as prose rather than deleted, because the entry was wrong in a way worth seeing. A plan can
require a form for a reason that never existed, the tag copies the requirement, and both look
correct to `azimuth check` — `Oracle` is descriptive and never gated, so nothing compares it to
anything. Found by the agent tier, not the machine tier.
