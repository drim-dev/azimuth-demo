# Verification: explicit-signed-quote-mechanism

## Claim: issued-token-round-trips
Quantification: universal
Oracle: direct

Generate valid payload identities, lifetimes, routes, policies, currencies and component sets. The
expected result is the generated payload rather than a value reconstructed by the codec.

## Claim: altered-token-rejected
Quantification: universal
Oracle: metamorphic

For every non-delimiter position in every generated token, substitute another base64url character
and require rejection while retaining the unmodified-token control. The member set comes from the
token emitted by the subject rather than a hand-written list.

## Claim: foreign-signature-rejected
Quantification: universal
Oracle: metamorphic

Generate distinct authority keys and payloads. The issuing authority must decode its own token and
the other authority must reject that same token.

## Residual: application-surface-not-enumerated
Accepted: until quote tokens acquire a compiler-visible boundary from which every consumer can be
derived, or a second token mechanism provides evidence for the common application relation

Implementation and mechanism evidence establish the control itself. Existing business evidence
demonstrates Trips and Payments apply it, but no independent source enumerates every present and
future quote-token consumer. A hand-written list is not accepted as evidence of completeness.
