# Verification: trip/request

## Claim: quote-consumed-once
Scope: component
Quantification: invariant

Single consumption is a uniqueness claim over concurrent requests, settled by storage. The
sequential version of this test passes against an implementation with no constraint.

## Claim: second-request-rejected-while-active
Scope: component
Quantification: invariant

"A rider holds at most one active trip" is the same shape as `captured-once`: two requests
arriving together are the case that matters, and they are only distinguishable against a real
store.

## Claim: request-rejected-with-unknown-quote
Oracle: contract

The rejection depends on how `pricing/quote` answers a lookup for an identifier it does not
recognise. Recorded because the failure mode is a disagreement between two services about what
"unknown" looks like, not a defect in either.
