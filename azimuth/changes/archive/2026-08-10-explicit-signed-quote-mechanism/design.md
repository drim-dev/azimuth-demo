# Design: explicit-signed-quote-mechanism

## Atomic mechanisms

`quote-token-issuance` is `QuoteTokenCodec.Encode`: it validates the payload, serializes it and
authenticates the encoded body with the configured key. `quote-token-validation` is
`QuoteTokenCodec.Decode`: it authenticates before deserializing or returning the payload. They are
separate identities because either operation can move independently and each design mechanism must
resolve to one implementation artifact.

The implementation tags derive symbol bindings. Current design therefore retains the expected
identity if a method or its tag is deleted, while a rename with the tag intact moves the derived
binding and expires judgments that read it.

## Evidence split

The positive round-trip checks issuance and validation together. Mutation evidence changes every
encoded body and signature position across generated payloads. Foreign-authority evidence varies
independent generated keys. The tests carry `CoversMechanism` for the control contract and `Covers`
only for the three concern claims; no mechanism tag is copied onto Pricing, Trips or Payments
scenarios.

## Application boundary

Trips admission and Payments capture already call `Decode`, and their existing claim evidence
fails when validation is removed. That demonstrates the two current applications but does not
enumerate the application domain. A list of known consumers would miss the first new path whose
author forgot both the call and the list entry. This change therefore records the gap instead of
adding an `AppliesMechanism` declaration or a catalog before an independent enumerator exists.

## Rejected alternatives

One mechanism bound to the whole codec was rejected because D27 requires one atomic implementation
site. Reusing `Realizes` for mechanism evidence was rejected because a test of HMAC behavior does
not cover every business consequence that depends on it. A hand-authored consumer list was rejected
because it would reproduce the omission it is supposed to detect.
